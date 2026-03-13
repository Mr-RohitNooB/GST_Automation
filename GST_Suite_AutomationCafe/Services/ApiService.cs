using GST_Suite_AutomationCafe.Models;
using System.Net.Http;
using System.Net.Http.Json;

namespace GST_Suite_AutomationCafe.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        // Context: Change 7045 to match your API's local port shown in Swagger
        private const string BaseUrl = "https://localhost:7045/api/";

        public ApiService()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        }

        public async Task<LoginResponse> LoginAsync(string email, string password)
        {
            var request = new LoginRequest { Email = email, Password = password };
            var response = await _httpClient.PostAsJsonAsync("Auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
                return loginResponse ?? throw new Exception("Received empty response from server.");
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Login Failed: {errorContent}");
        }

        #region Secure Module Fetching
        public async Task<string> GetModuleDownloadUrlAsync(int moduleId, string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.GetAsync($"Modules/download/{moduleId}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                return content.GetProperty("downloadUrl").GetString() ?? "";
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Access Denied: {errorContent}");
        }

        public async Task<string> DownloadScriptJsonAsync(string cdnUrl)
        {
            await Task.Delay(500);

            return """
            {
              "moduleName": "GST Downloader",
              "version": "1.0",
              "steps": [
                { "action": "goto", "value": "https://services.gst.gov.in/services/login" },
                { "action": "type", "selector": "#username", "value": "{GST_USERNAME}" },
                { "action": "type", "selector": "#user_pass", "value": "{GST_PASSWORD}" },
                { "action": "wait", "value": "3000" }
              ]
            }
            """;
        }
        #endregion
    }
}