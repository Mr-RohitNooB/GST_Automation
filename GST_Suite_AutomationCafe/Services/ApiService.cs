using GST_Suite_AutomationCafe.Models;
using System.Net.Http;
using System.Net.Http.Json;

namespace GST_Suite_AutomationCafe.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        // Context: Change 7045 to match your API's local port shown in Swagger
        private const string BaseUrl = "http://52.66.105.234/api/";

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
        // Fetches the JSON automation config for a module from the DB via API.
        // When the GST portal changes layout, only the DB record needs updating — no client rebuild.
        public async Task<string> GetModuleScriptAsync(int moduleId, string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync($"Modules/script/{moduleId}");

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            throw new Exception($"Failed to fetch module script. Status: {response.StatusCode}");
        }

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
            var response = await _httpClient.GetAsync(cdnUrl);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            throw new Exception($"Failed to download script from server. Status: {response.StatusCode}");
        }
        #endregion
    }
}