namespace GST_Suite_AutomationCafe.Models
{
    #region JSON Schema Models (For Playwright)
    // Context: Represents the entire downloaded JSON file for a tool like "GST Downloader"
    public class AutomationScript
    {
        public required string ModuleName { get; set; }
        public required string Version { get; set; }
        public required List<AutomationStep> Steps { get; set; }
    }

    // Context: Represents a single instruction in the JSON array
    public class AutomationStep
    {
        public required string Action { get; set; }     // e.g., "goto", "type", "click", "wait"
        public string? Selector { get; set; }           // e.g., "#username", "//button[@id='submit']"
        public string? Value { get; set; }              // e.g., "https://gst.gov.in", "{USER_INPUT_ID}"
    }
    #endregion

    #region API Communication Models (For Login)
    // Context: The exact shape of the JSON we send to your ASP.NET Core API
    public class LoginRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }

    // Context: The exact shape of the JSON we get BACK from your API (containing the Token)
    public class LoginResponse
    {
        public required string Token { get; set; }
        public Guid SessionId { get; set; }
        public required string Message { get; set; }
    }
    #endregion
}