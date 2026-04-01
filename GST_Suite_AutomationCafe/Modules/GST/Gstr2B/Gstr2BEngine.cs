using ClosedXML.Excel;
using GST_Suite_AutomationCafe.Models;
using GST_Suite_AutomationCafe.Services;
using Microsoft.Playwright;
using System.IO;

namespace GST_Suite_AutomationCafe.Modules.GST.Gstr2B
{
    public class Gstr2BSettings
    {
        public string FinancialYear { get; set; } = "";
        public string Quarter       { get; set; } = "";
        public string Month         { get; set; } = "";
        public bool   AllQuarters   { get; set; } = false;
    }

    public class Gstr2BEngine
    {
        public event Action<string>?              LogMessage;
        public event Action<double>?              ProgressChanged;
        public event Func<byte[], Task<string>>?  CaptchaRequired;

        private bool _keepRunning;

        private static readonly Dictionary<string, string[]> QuarterMap = new()
        {
            ["Quarter 1 (Apr - Jun)"] = ["April",   "May",      "June"],
            ["Quarter 2 (Jul - Sep)"] = ["July",    "August",   "September"],
            ["Quarter 3 (Oct - Dec)"] = ["October", "November", "December"],
            ["Quarter 4 (Jan - Mar)"] = ["January", "February", "March"]
        };

        public void Stop() => _keepRunning = false;

        // config comes from GET /api/modules/script/{moduleId} — no hardcoded selectors here
        public async Task RunAsync(
            List<CredentialEntry> credentials,
            Gstr2BSettings settings,
            ModuleScriptConfig config,
            CancellationToken ct)
        {
            _keepRunning = true;
            var report = new List<ReportRow>();

            var downloadBase = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AutomationCafe", "Downloads", "GSTR-2B");
            Directory.CreateDirectory(downloadBase);

            Log($"🚀 GSTR-2B Engine — Script v{config.Version}");
            Log($"📁 Saving to: {downloadBase}");

            for (int i = 0; i < credentials.Count; i++)
            {
                if (!_keepRunning || ct.IsCancellationRequested) break;

                var cred     = credentials[i];
                var password = CredentialStore.Decrypt(cred.EncryptedPassword);

                Log($"\n🔹 [{i + 1}/{credentials.Count}] {cred.DisplayName} ({cred.Username})");
                Progress((double)i / credentials.Count);

                var userFolder = GetUniqueFolder(
                    Path.Combine(downloadBase, SanitizeName(cred.Username)));

                var (status, details) = await ProcessSingleUserAsync(
                    cred.Username, password, userFolder, settings, config, ct);

                report.Add(new ReportRow
                {
                    DisplayName = cred.DisplayName,
                    Username    = cred.Username,
                    Status      = status,
                    Details     = details,
                    Timestamp   = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    SavedTo     = userFolder
                });

                Log($"   ─── Result: {status} — {details}");
                Log("─────────────────────────────────────");
            }

            Progress(1.0);
            SaveReport(report, downloadBase);
            Log("\n✅ All tasks completed.");
        }

        private async Task<(string status, string details)> ProcessSingleUserAsync(
            string username, string password, string downloadFolder,
            Gstr2BSettings settings, ModuleScriptConfig cfg, CancellationToken ct)
        {
            IPlaywright? playwright = null;
            IBrowser?    browser    = null;

            try
            {
                playwright = await Playwright.CreateAsync();
                browser    = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = false,
                    Args     = new[] { "--disable-blink-features=AutomationControlled" }
                });

                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent      = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36",
                    AcceptDownloads = true
                });

                var page = await context.NewPageAsync();
                await page.AddInitScriptAsync(
                    "Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

                var (loginOk, loginMsg) = await PerformLoginAsync(page, username, password, cfg.Login, ct);
                if (!loginOk) return ("Login Failed", loginMsg);

                var tasks = BuildTaskList(settings);
                if (tasks.Count == 0) return ("Config Error", "No tasks from settings");

                var yearFolder = Path.Combine(downloadFolder, settings.FinancialYear);
                Directory.CreateDirectory(yearFolder);

                int successCount = 0;
                var results      = new List<string>();

                foreach (var task in tasks)
                {
                    if (!_keepRunning || ct.IsCancellationRequested)
                        return ("Stopped", "User cancelled");

                    bool   monthSuccess = false;
                    string failReason   = "";

                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        Log($"   ⚙️ {task.Month} (Attempt {attempt})");
                        try
                        {
                            if (!await CheckSessionAsync(page, username, password, cfg, ct))
                            { failReason = "Re-login failed"; break; }

                            if (attempt > 1)
                            {
                                await page.GotoAsync(cfg.Login.DashboardUrl);
                                await Task.Delay(4000, ct);
                                if (!await CheckSessionAsync(page, username, password, cfg, ct))
                                { failReason = "Re-login failed after reset"; break; }
                            }

                            // Select period using selectors from config
                            await page.SelectOptionAsync(cfg.Period.YearSelector, settings.FinancialYear);
                            await Task.Delay(500, ct);

                            await page.SelectOptionAsync(cfg.Period.QuarterSelector,
                                new SelectOptionValue { Label = task.Quarter });
                            await Task.Delay(500, ct);

                            await page.SelectOptionAsync(cfg.Period.MonthSelector,
                                new SelectOptionValue { Label = task.Month });
                            await Task.Delay(500, ct);

                            await page.ClickAsync(cfg.Period.SearchSelector);
                            await Task.Delay(4000, ct);

                            (bool dlOk, string dlMsg) = await DownloadAsync(page, yearFolder, cfg.Download, ct);

                            if (dlOk)
                            {
                                monthSuccess = true;
                                successCount++;
                                results.Add($"{task.Month}: ✅");
                                break;
                            }

                            failReason = dlMsg;
                            if (dlMsg == "Not Generated") break;
                            Log($"      ⚠️ Attempt {attempt} failed: {dlMsg}");
                        }
                        catch (Exception ex)
                        {
                            failReason = $"Error: {Clip(ex.Message, 30)}";
                            Log($"      ❌ {Clip(ex.Message, 50)}");
                            try { await page.GotoAsync(cfg.Login.DashboardUrl); } catch { }
                        }
                    }

                    if (!monthSuccess) results.Add($"{task.Month}: ❌ ({failReason})");
                }

                string overall = successCount == tasks.Count ? "Success"
                               : successCount == 0           ? "Failed" : "Partial";

                return (overall, $"Downloaded {successCount}/{tasks.Count}. " + string.Join(", ", results));
            }
            catch (Exception ex)
            {
                return ("Error", $"Browser crash: {Clip(ex.Message, 30)}");
            }
            finally
            {
                if (browser != null) await browser.CloseAsync();
                playwright?.Dispose();
            }
        }

        private async Task<(bool ok, string msg)> PerformLoginAsync(
            IPage page, string username, string password,
            LoginConfig login, CancellationToken ct)
        {
            Log("   🌐 Opening GST Portal...");
            await page.GotoAsync(login.Url);

            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (!_keepRunning || ct.IsCancellationRequested) return (false, "Stopped");

                try
                {
                    await page.WaitForSelectorAsync(login.UsernameSelector, new() { Timeout = 20000 });
                    await page.FillAsync(login.UsernameSelector, username);
                    await page.FillAsync(login.PasswordSelector, password);

                    Log("   📸 Capturing CAPTCHA...");
                    var captchaBytes = await page.Locator(login.CaptchaImageSelector).ScreenshotAsync();

                    if (CaptchaRequired == null) return (false, "No captcha handler");

                    Log("   ⌨️ Waiting for CAPTCHA input...");
                    string captchaText = await CaptchaRequired.Invoke(captchaBytes);
                    if (string.IsNullOrEmpty(captchaText)) return (false, "CAPTCHA cancelled");

                    await page.FillAsync(login.CaptchaInputSelector, captchaText);
                    await page.ClickAsync(login.SubmitSelector);
                    await Task.Delay(3000, ct);

                    var src = await page.ContentAsync();

                    if (src.Contains(login.InvalidCredText))
                    {
                        Log("   ❌ Invalid credentials.");
                        return (false, "Invalid credentials");
                    }

                    if (src.Contains(login.InvalidCaptchaText))
                    {
                        Log("   ⚠️ Invalid CAPTCHA — retrying...");
                        continue;
                    }

                    var title = await page.TitleAsync();
                    if (title.Contains("Dashboard") || src.Contains(login.SuccessText))
                    {
                        Log("   ✅ Login successful!");
                        await Task.Delay(2000, ct);

                        // Dismiss popups using selectors from config
                        await TryDismiss(page, login.AadharSkipSelector,  "Aadhaar popup",  ct);
                        await TryDismiss(page, login.GenericSkipSelector, "Generic popup",  ct);

                        // Navigate to Return Dashboard
                        try
                        {
                            await page.ClickAsync(login.ReturnDashboardSelector, new() { Timeout = 10000 });
                            return (true, "Success");
                        }
                        catch { return (false, "Dashboard nav failed"); }
                    }
                }
                catch (Exception ex)
                {
                    Log($"   ⚠️ Login error: {Clip(ex.Message, 30)}");
                    return (false, $"Login error: {Clip(ex.Message, 20)}");
                }
            }

            return (false, "Max CAPTCHA attempts reached");
        }

        private async Task<(bool ok, string msg)> DownloadAsync(
            IPage page, string downloadFolder,
            DownloadConfig dl, CancellationToken ct)
        {
            Log("   🔍 Looking for GSTR-2B tile...");

            ILocator? downloadBtn = null;

            // Priority 1: quarterly view tile
            try
            {
                var btn = page.Locator(dl.QuarterlyTileXpath);
                if (await btn.IsVisibleAsync()) { downloadBtn = btn; Log("   ✅ Found GSTR-2BQ tile."); }
            }
            catch { }

            // Priority 2: standard tile
            if (downloadBtn == null)
            {
                try
                {
                    var btn = page.Locator(dl.StandardTileXpath);
                    if (await btn.IsVisibleAsync()) { downloadBtn = btn; Log("   ✅ Found GSTR-2B tile."); }
                }
                catch { }
            }

            if (downloadBtn == null)
            {
                Log("   ⚠️ No GSTR-2B tile found.");
                return (false, "Tile Missing");
            }

            try
            {
                await downloadBtn.ClickAsync(new LocatorClickOptions { Force = true });
                await Task.Delay(4000, ct);

                var src = await page.ContentAsync();
                if (src.Contains(dl.NoRecordText) || src.Contains(dl.NotGeneratedText))
                {
                    Log("   ⚠️ GSTR-2B not generated for this period.");
                    await page.GoBackAsync();
                    return (false, "Not Generated");
                }

                var genBtn = page.Locator(dl.GenerateBtnXpath);
                await genBtn.WaitForAsync(new() { Timeout = 10000 });
                Log("   ⬇️ Clicking Generate Excel...");

                var downloadTask    = page.WaitForDownloadAsync();
                await genBtn.ClickAsync(new LocatorClickOptions { Force = true });

                bool fileDownloaded = false;

                if (await Task.WhenAny(downloadTask, Task.Delay(25000, ct)) == downloadTask)
                {
                    var d = await downloadTask;
                    await d.SaveAsAsync(Path.Combine(downloadFolder, d.SuggestedFilename));
                    Log($"   ✅ Saved: {d.SuggestedFilename}");
                    fileDownloaded = true;
                }
                else
                {
                    // Fallback: poll for download link
                    for (int i = 0; i < 20 && !fileDownloaded; i++)
                    {
                        await Task.Delay(1000, ct);
                        try
                        {
                            var link = page.Locator(dl.DownloadLinkSelector);
                            if (await link.IsVisibleAsync())
                            {
                                var dlTask = page.WaitForDownloadAsync();
                                await link.ClickAsync();
                                var d = await dlTask;
                                await d.SaveAsAsync(Path.Combine(downloadFolder, d.SuggestedFilename));
                                Log($"   ✅ Saved via link: {d.SuggestedFilename}");
                                fileDownloaded = true;
                            }
                        }
                        catch { }
                    }
                }

                await page.GoBackAsync();
                return fileDownloaded ? (true, "Success") : (false, "Timeout");
            }
            catch (Exception ex)
            {
                try { await page.GoBackAsync(); } catch { }
                return (false, $"Script error: {Clip(ex.Message, 20)}");
            }
        }

        private async Task<bool> CheckSessionAsync(
            IPage page, string username, string password,
            ModuleScriptConfig cfg, CancellationToken ct)
        {
            try
            {
                var src = await page.ContentAsync();
                var url = page.Url;

                bool expired =
                    src.Contains(cfg.Session.AccessDeniedText)  ||
                    src.Contains(cfg.Session.ExpiredText)        ||
                    url.Contains(cfg.Session.ExpiredUrlFragment) ||
                    await page.Locator(cfg.Session.LoginPageIndicator).CountAsync() > 0;

                if (expired)
                {
                    Log("   🔄 Session expired — re-logging in...");
                    var (ok, msg) = await PerformLoginAsync(page, username, password, cfg.Login, ct);
                    if (!ok) { Log($"   ❌ Re-login failed: {msg}"); return false; }
                    Log("   ✅ Re-login successful.");
                }
                return true;
            }
            catch { return true; }
        }

        private async Task TryDismiss(IPage page, string selector, string label, CancellationToken ct)
        {
            try
            {
                var el = page.Locator(selector);
                if (await el.IsVisibleAsync())
                {
                    await el.ClickAsync();
                    await Task.Delay(1500, ct);
                    Log($"   ℹ️ {label} dismissed.");
                }
            }
            catch { }
        }

        private static List<(string Quarter, string Month)> BuildTaskList(Gstr2BSettings s)
        {
            var tasks = new List<(string, string)>();

            if (s.AllQuarters)
            {
                foreach (var (q, months) in QuarterMap)
                    foreach (var m in months)
                        tasks.Add((q, m));
            }
            else if (s.Month == "Whole Quarter" && QuarterMap.TryGetValue(s.Quarter, out var qm))
            {
                foreach (var m in qm) tasks.Add((s.Quarter, m));
            }
            else
            {
                tasks.Add((s.Quarter, s.Month));
            }

            return tasks;
        }

        private void SaveReport(List<ReportRow> rows, string folder)
        {
            try
            {
                if (rows.Count == 0) return;
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("GSTR-2B Report");

                string[] headers = ["Display Name", "Username", "Status", "Details", "Timestamp", "Saved To"];
                for (int c = 0; c < headers.Length; c++)
                {
                    ws.Cell(1, c + 1).Value = headers[c];
                    ws.Cell(1, c + 1).Style.Font.Bold = true;
                    ws.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.DarkBlue;
                    ws.Cell(1, c + 1).Style.Font.FontColor = XLColor.White;
                }

                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    ws.Cell(i + 2, 1).Value = r.DisplayName;
                    ws.Cell(i + 2, 2).Value = r.Username;
                    ws.Cell(i + 2, 3).Value = r.Status;
                    ws.Cell(i + 2, 4).Value = r.Details;
                    ws.Cell(i + 2, 5).Value = r.Timestamp;
                    ws.Cell(i + 2, 6).Value = r.SavedTo;

                    ws.Cell(i + 2, 3).Style.Fill.BackgroundColor = r.Status switch
                    {
                        "Success" => XLColor.LightGreen,
                        "Partial" => XLColor.LightYellow,
                        _         => XLColor.LightPink
                    };
                }

                ws.Columns().AdjustToContents();
                var path = Path.Combine(folder, $"GSTR2B_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                wb.SaveAs(path);
                Log($"📄 Report saved: {path}");
            }
            catch (Exception ex) { Log($"⚠️ Report save failed: {ex.Message}"); }
        }

        private static string GetUniqueFolder(string basePath)
        {
            if (!Directory.Exists(basePath)) return basePath;
            int i = 1;
            while (Directory.Exists($"{basePath}_{i}")) i++;
            return $"{basePath}_{i}";
        }

        private static string SanitizeName(string name) =>
            string.Concat(name.Split(Path.GetInvalidFileNameChars()));

        private static string Clip(string s, int max) =>
            s.Length <= max ? s : s[..max];

        private void Log(string msg)      => LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}");
        private void Progress(double val) => ProgressChanged?.Invoke(val);
    }

    public class ReportRow
    {
        public string DisplayName { get; set; } = "";
        public string Username    { get; set; } = "";
        public string Status      { get; set; } = "";
        public string Details     { get; set; } = "";
        public string Timestamp   { get; set; } = "";
        public string SavedTo     { get; set; } = "";
    }
}
