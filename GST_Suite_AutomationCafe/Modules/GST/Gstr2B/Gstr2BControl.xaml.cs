using GST_Suite_AutomationCafe.Models;
using GST_Suite_AutomationCafe.Services;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GST_Suite_AutomationCafe.Modules.GST.Gstr2B
{
    public partial class Gstr2BControl : UserControl
    {
        // ── View model for each credential row (adds IsSelected) ──────────────
        private class CredentialVM
        {
            public Guid Id { get; set; }
            public string DisplayName { get; set; } = "";
            public string Username { get; set; } = "";
            public bool IsSelected { get; set; } = true;
        }

        private readonly ObservableCollection<CredentialVM> _credentials = new();
        private CancellationTokenSource? _cts;
        private TaskCompletionSource<string>? _captchaTcs;

        public Gstr2BControl()
        {
            InitializeComponent();
            CredentialsList.ItemsSource = _credentials;
            LoadCredentials();
            InitYearCombo();
            InitMonthCombo("Quarter 1 (Apr - Jun)");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CREDENTIALS
        // ══════════════════════════════════════════════════════════════════════

        private void LoadCredentials()
        {
            _credentials.Clear();
            foreach (var e in CredentialStore.Load())
            {
                _credentials.Add(new CredentialVM
                {
                    Id = e.Id,
                    DisplayName = e.DisplayName,
                    Username = e.Username,
                    IsSelected = true
                });
            }
        }

        private void ShowAddFormBtn_Click(object sender, RoutedEventArgs e)
        {
            DisplayNameInput.Text = "";
            UsernameInput.Text = "";
            PasswordInput.Password = "";
            AddCredentialForm.Visibility = Visibility.Visible;
            ShowAddFormBtn.Visibility = Visibility.Collapsed;
            DisplayNameInput.Focus();
        }

        private void CancelAddCredential_Click(object sender, RoutedEventArgs e)
        {
            AddCredentialForm.Visibility = Visibility.Collapsed;
            ShowAddFormBtn.Visibility = Visibility.Visible;
        }

        private void SaveCredential_Click(object sender, RoutedEventArgs e)
        {
            var displayName = DisplayNameInput.Text.Trim();
            var username = UsernameInput.Text.Trim();
            var password = PasswordInput.Password;

            if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("All fields are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var all = CredentialStore.Load();
            all.Add(new CredentialEntry
            {
                DisplayName = displayName,
                Username = username,
                EncryptedPassword = CredentialStore.Encrypt(password)
            });
            CredentialStore.Save(all);

            LoadCredentials();
            AddCredentialForm.Visibility = Visibility.Collapsed;
            ShowAddFormBtn.Visibility = Visibility.Visible;
        }

        private void DeleteCredential_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid id)
            {
                var all = CredentialStore.Load();
                all.RemoveAll(x => x.Id == id);
                CredentialStore.Save(all);
                LoadCredentials();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PERIOD SETTINGS
        // ══════════════════════════════════════════════════════════════════════

        private void InitYearCombo()
        {
            int curYear = DateTime.Now.Year;
            int startYear = DateTime.Now.Month < 4 ? curYear - 1 : curYear;
            var years = Enumerable.Range(startYear - 2, 5)
                .Select(y => $"{y}-{(y + 1).ToString()[^2..]}").ToList();

            YearCombo.ItemsSource = years;
            YearCombo.SelectedIndex = 0;
        }

        private void InitMonthCombo(string quarter)
        {
            var months = quarter switch
            {
                "Quarter 1 (Apr - Jun)" => new[] { "Whole Quarter", "April", "May", "June" },
                "Quarter 2 (Jul - Sep)" => new[] { "Whole Quarter", "July", "August", "September" },
                "Quarter 3 (Oct - Dec)" => new[] { "Whole Quarter", "October", "November", "December" },
                _ => new[] { "Whole Quarter", "January", "February", "March" }
            };
            MonthCombo.ItemsSource = months;
            MonthCombo.SelectedIndex = 0;
        }

        private void QuarterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QuarterCombo.SelectedItem is ComboBoxItem item)
                InitMonthCombo(item.Content.ToString() ?? "");
        }

        private void AllQuarters_Changed(object sender, RoutedEventArgs e)
        {
            bool all = AllQuartersChk.IsChecked == true;
            QuarterCombo.IsEnabled = !all;
            MonthCombo.IsEnabled = !all;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  START / STOP
        // ══════════════════════════════════════════════════════════════════════

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = _credentials.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one credential.", "No Credentials",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (YearCombo.SelectedItem is null)
            {
                MessageBox.Show("Please select a financial year.", "No Year",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = new Gstr2BSettings
            {
                FinancialYear = YearCombo.SelectedItem.ToString()!,
                Quarter       = (QuarterCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "",
                Month         = MonthCombo.SelectedItem?.ToString() ?? "",
                AllQuarters   = AllQuartersChk.IsChecked == true
            };

            var allStored        = CredentialStore.Load();
            var selectedIds      = selected.Select(c => c.Id).ToHashSet();
            var credentialsToRun = allStored.Where(e => selectedIds.Contains(e.Id)).ToList();

            SetRunningState(true);
            LogBox.Clear();
            ProgressBar.Value = 0;
            StatusText.Text   = "Fetching latest script from server...";

            // ── Fetch automation config from API (all selectors live here, not in code) ──
            ModuleScriptConfig config;
            try
            {
                var api        = new Services.ApiService();
                var scriptJson = await api.GetModuleScriptAsync(1, MainWindow.CurrentJwtToken);
                config = JsonSerializer.Deserialize<ModuleScriptConfig>(scriptJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new Exception("Failed to parse module script.");

                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] 📡 Script v{config.Version} loaded from server.\n");
                LogBox.ScrollToEnd();
            }
            catch (Exception ex)
            {
                SetRunningState(false);
                MessageBox.Show($"Could not load automation script from server:\n{ex.Message}",
                    "Script Load Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _cts = new CancellationTokenSource();
            var engine = new Gstr2BEngine();

            engine.LogMessage += msg => Dispatcher.Invoke(() =>
            {
                LogBox.AppendText(msg + "\n");
                LogBox.ScrollToEnd();
            });

            engine.ProgressChanged += val => Dispatcher.Invoke(() =>
                ProgressBar.Value = val * 100);

            engine.CaptchaRequired += async (imageBytes) =>
            {
                _captchaTcs = new TaskCompletionSource<string>();
                Dispatcher.Invoke(() => ShowCaptchaOverlay(imageBytes));
                return await _captchaTcs.Task;
            };

            try
            {
                await engine.RunAsync(credentialsToRun, settings, config, _cts.Token);
                StatusText.Text = "Completed";
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Stopped by user";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error occurred";
                LogBox.AppendText($"\n❌ Fatal error: {ex.Message}\n");
                LogBox.ScrollToEnd();
            }
            finally
            {
                SetRunningState(false);
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _captchaTcs?.TrySetResult("");
            HideCaptchaOverlay();
            StatusText.Text = "Stopping...";
        }

        private void SetRunningState(bool running)
        {
            StartBtn.IsEnabled = !running;
            StopBtn.IsEnabled = running;
            StatusText.Text = running ? "Running..." : "";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CAPTCHA OVERLAY
        // ══════════════════════════════════════════════════════════════════════

        private void ShowCaptchaOverlay(byte[] imageBytes)
        {
            using var ms = new System.IO.MemoryStream(imageBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            CaptchaImage.Source = bitmap;
            CaptchaInput.Text = "";
            CaptchaOverlay.Visibility = Visibility.Visible;
            CaptchaInput.Focus();
        }

        private void HideCaptchaOverlay()
        {
            CaptchaOverlay.Visibility = Visibility.Collapsed;
            CaptchaInput.Text = "";
        }

        private void SubmitCaptcha_Click(object sender, RoutedEventArgs e) => SubmitCaptcha();

        private void CaptchaInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SubmitCaptcha();
        }

        private void SubmitCaptcha()
        {
            var text = CaptchaInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            HideCaptchaOverlay();
            _captchaTcs?.TrySetResult(text);
        }
    }
}
