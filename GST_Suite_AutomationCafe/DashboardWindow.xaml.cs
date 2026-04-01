using GST_Suite_AutomationCafe.Modules.GST.Gstr2B;
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
        private record ModuleInfo(int Id, string Name);

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

                var modulesClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "allowed_modules")?.Value;

                if (string.IsNullOrEmpty(modulesClaim))
                {
                    AddNoAccessMessage();
                    return;
                }

                var modules = JsonSerializer.Deserialize<List<ModuleInfo>>(modulesClaim,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new List<ModuleInfo>();

                if (modules.Count == 0)
                {
                    AddNoAccessMessage();
                    return;
                }

                foreach (var module in modules)
                {
                    Button toolBtn = CreateToolButton(module.Id, module.Name);
                    ModuleContainer.Children.Add(toolBtn);
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

            btn.Click += (s, e) => LoadModuleControl(moduleName);
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

        #region Module Routing
        private void LoadModuleControl(string moduleName)
        {
            var name = moduleName.ToLowerInvariant();

            System.Windows.Controls.UserControl? control = name switch
            {
                var n when n.Contains("2b") || n.Contains("gstr2b") => new Gstr2BControl(),
                _ => null
            };

            if (control != null)
            {
                ModuleContent.Content = control;
            }
            else
            {
                MessageBox.Show($"Module '{moduleName}' is not yet available in this version.",
                    "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion
    }
}