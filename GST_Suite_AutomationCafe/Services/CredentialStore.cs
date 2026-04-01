using GST_Suite_AutomationCafe.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GST_Suite_AutomationCafe.Services
{
    public static class CredentialStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutomationCafe", "credentials.json");

        public static List<CredentialEntry> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<CredentialEntry>>(json) ?? new();
            }
            catch { return new(); }
        }

        public static void Save(List<CredentialEntry> entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(entries,
                new JsonSerializerOptions { WriteIndented = true }));
        }

        public static string Encrypt(string plainText)
        {
            var bytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }

        public static string Decrypt(string cipherText)
        {
            try
            {
                var bytes = ProtectedData.Unprotect(
                    Convert.FromBase64String(cipherText), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch { return ""; }
        }
    }
}
