using System.Text.Json.Serialization;

namespace GST_Suite_AutomationCafe.Models
{
    // Root config — deserialized from JsonContent column in AutomationScripts table
    public class ModuleScriptConfig
    {
        [JsonPropertyName("moduleName")]   public string ModuleName { get; set; } = "";
        [JsonPropertyName("version")]      public string Version    { get; set; } = "";
        [JsonPropertyName("login")]        public LoginConfig    Login    { get; set; } = new();
        [JsonPropertyName("period")]       public PeriodConfig   Period   { get; set; } = new();
        [JsonPropertyName("download")]     public DownloadConfig Download { get; set; } = new();
        [JsonPropertyName("session")]      public SessionConfig  Session  { get; set; } = new();
    }

    public class LoginConfig
    {
        [JsonPropertyName("url")]                    public string Url                    { get; set; } = "";
        [JsonPropertyName("usernameSelector")]       public string UsernameSelector       { get; set; } = "";
        [JsonPropertyName("passwordSelector")]       public string PasswordSelector       { get; set; } = "";
        [JsonPropertyName("captchaImageSelector")]   public string CaptchaImageSelector   { get; set; } = "";
        [JsonPropertyName("captchaInputSelector")]   public string CaptchaInputSelector   { get; set; } = "";
        [JsonPropertyName("submitSelector")]         public string SubmitSelector         { get; set; } = "";
        [JsonPropertyName("successText")]            public string SuccessText            { get; set; } = "";
        [JsonPropertyName("invalidCaptchaText")]     public string InvalidCaptchaText     { get; set; } = "";
        [JsonPropertyName("invalidCredText")]        public string InvalidCredText        { get; set; } = "";
        [JsonPropertyName("aadharSkipSelector")]     public string AadharSkipSelector     { get; set; } = "";
        [JsonPropertyName("genericSkipSelector")]    public string GenericSkipSelector    { get; set; } = "";
        [JsonPropertyName("returnDashboardSelector")]public string ReturnDashboardSelector{ get; set; } = "";
        [JsonPropertyName("dashboardUrl")]           public string DashboardUrl           { get; set; } = "";
    }

    public class PeriodConfig
    {
        [JsonPropertyName("yearSelector")]    public string YearSelector    { get; set; } = "";
        [JsonPropertyName("quarterSelector")] public string QuarterSelector { get; set; } = "";
        [JsonPropertyName("monthSelector")]   public string MonthSelector   { get; set; } = "";
        [JsonPropertyName("searchSelector")]  public string SearchSelector  { get; set; } = "";
    }

    public class DownloadConfig
    {
        [JsonPropertyName("quarterlyTileXpath")]  public string QuarterlyTileXpath  { get; set; } = "";
        [JsonPropertyName("standardTileXpath")]   public string StandardTileXpath   { get; set; } = "";
        [JsonPropertyName("generateBtnXpath")]    public string GenerateBtnXpath    { get; set; } = "";
        [JsonPropertyName("downloadLinkSelector")]public string DownloadLinkSelector{ get; set; } = "";
        [JsonPropertyName("notGeneratedText")]    public string NotGeneratedText    { get; set; } = "";
        [JsonPropertyName("noRecordText")]        public string NoRecordText        { get; set; } = "";
    }

    public class SessionConfig
    {
        [JsonPropertyName("accessDeniedText")]   public string AccessDeniedText   { get; set; } = "";
        [JsonPropertyName("expiredText")]         public string ExpiredText         { get; set; } = "";
        [JsonPropertyName("expiredUrlFragment")]  public string ExpiredUrlFragment  { get; set; } = "";
        [JsonPropertyName("loginPageIndicator")] public string LoginPageIndicator  { get; set; } = "";
    }
}
