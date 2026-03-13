using GST_Suite_AutomationCafe.Services;
using System.Windows;

namespace GST_Suite_AutomationCafe
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService;

        // Context: We store the token here temporarily so the Dashboard can use it to fetch modules.
        public static string CurrentJwtToken { get; private set; } = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                txtStatus.Text = "Please enter both email and password.";
                return;
            }

            // Update UI to show loading state
            btnLogin.IsEnabled = false;
            btnLogin.Content = "AUTHENTICATING...";
            txtStatus.Text = "";
            txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)); // Blue

            try
            {
                // Call the API
                var response = await _apiService.LoginAsync(email, password);

                // Store the token globally for this session
                CurrentJwtToken = response.Token;

                txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74)); // Green
                txtStatus.Text = "Login Successful! Connecting to server...";

                // --- Transition to the Dashboard ---
                var dashboard = new DashboardWindow();
                dashboard.Show();
                this.Close(); // Closes the login window
            }
            catch (Exception ex)
            {
                // Show errors (like Invalid Password or Max Devices Reached)
                txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38)); // Red
                txtStatus.Text = ex.Message;
            }
            finally
            {
                // Reset UI
                if (btnLogin != null)
                {
                    btnLogin.IsEnabled = true;
                    btnLogin.Content = "SECURE LOGIN";
                }
            }
        }
    }
}