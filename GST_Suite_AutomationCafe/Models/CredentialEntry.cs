namespace GST_Suite_AutomationCafe.Models
{
    public class CredentialEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DisplayName { get; set; } = "";
        public string Username { get; set; } = "";
        public string EncryptedPassword { get; set; } = "";
    }
}
