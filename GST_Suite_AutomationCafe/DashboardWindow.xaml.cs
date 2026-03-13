using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GST_Suite_AutomationCafe
{
    public partial class DashboardWindow : Window
    {
        private readonly Dictionary<int, string> _moduleCatalog = new()
        {
            { 1, "GST Portal Downloader" },
            { 2, "ITR Auto-Filer" },
            { 3, "LinkedIn Scraper" }
        };

        public DashboardWindow()
        {
            InitializeComponent();
            LoadUserModules();
        }

        #region UI Generation
        private void LoadUserModules()
        {
            try
            {
                ModuleContainer.Children.Clear();

                string token = MainWindow.CurrentJwtToken;
                if (string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("Security Error: No active session found.");
                    return;
                }

                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // --- DEBUG TOOL: SHOW ME THE CONTENTS OF THE TOKEN ---
                string allClaims = string.Join("\n", jwtToken.Claims.Select(c => c.Type + " = " + c.Value));
                MessageBox.Show(allClaims, "What is inside my Token?");
                // -----------------------------------------------------

                var modulesClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "allowed_modules")?.Value;

                if (string.IsNullOrEmpty(modulesClaim))
                {
                    AddNoAccessMessage();
                    return;
                }

                List<int> allowedModuleIds = JsonSerializer.Deserialize<List<int>>(modulesClaim) ?? new List<int>();

                if (allowedModuleIds.Count == 0)
                {
                    AddNoAccessMessage();
                    return;
                }

                foreach (int moduleId in allowedModuleIds)
                {
                    if (_moduleCatalog.TryGetValue(moduleId, out string? moduleName))
                    {
                        Button toolBtn = CreateToolButton(moduleId, moduleName);
                        ModuleContainer.Children.Add(toolBtn);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading dashboard: {ex.Message}");
            }
        }

        private Button CreateToolButton(int moduleId, string moduleName)
        {
            Button btn = new Button
            {
                Content = moduleName,
                Tag = moduleId,
                Height = 40,
                Margin = new Thickness(0, 0, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                Foreground = Brushes.White,
                FontSize = 14,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };

            btn.Click += async (s, e) => await TriggerAutomationEngine(moduleId, moduleName);
            return btn;
        }

        private void AddNoAccessMessage()
        {
            ModuleContainer.Children.Add(new TextBlock
            {
                Text = "No active subscriptions found.",
                Foreground = Brushes.Red,
                FontStyle = FontStyles.Italic
            });
        }
        #endregion

        #region Automation Trigger
        // Context: This is where the magic happens when you click the GST button!
        private async Task TriggerAutomationEngine(int moduleId, string moduleName)
        {
            try
            {
                var originalCursor = Cursor;
                Cursor = Cursors.Wait;

                var apiService = new Services.ApiService();
                var engine = new Services.AutomationEngine();

                // 1. Get the secure link
                string cdnUrl = await apiService.GetModuleDownloadUrlAsync(moduleId, MainWindow.CurrentJwtToken);

                // 2. Download the JSON Script
                string jsonScript = await apiService.DownloadScriptJsonAsync(cdnUrl);

                // 3. Prepare user credentials
                var userInputs = new Dictionary<string, string>
                {
                    { "GST_USERNAME", "Rohit_TaxPro_01" },
                    { "GST_PASSWORD", "SecurePassword123!" }
                };

                // 4. Run Playwright
                await engine.RunScriptAsync(jsonScript, userInputs);

                Cursor = originalCursor;
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Arrow;
                MessageBox.Show(ex.Message, "Automation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}