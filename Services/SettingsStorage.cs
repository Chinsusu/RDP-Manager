using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class SettingsStorage
    {
        public static AppSettings Load()
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            try
            {
                var serializer = new XmlSerializer(typeof(AppSettings));
                using (var stream = File.OpenRead(path))
                {
                    var settings = serializer.Deserialize(stream) as AppSettings;
                    return settings ?? new AppSettings();
                }
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            var path = GetSettingsPath();
            EnsureApplicationDataDirectory();

            var serializer = new XmlSerializer(typeof(AppSettings));
            using (var stream = File.Create(path))
            {
                serializer.Serialize(stream, settings ?? new AppSettings());
            }
        }

        public static string LoadCloudminiToken(AppSettings settings)
        {
            if (settings == null || !settings.RememberCloudminiToken || string.IsNullOrWhiteSpace(settings.EncryptedCloudminiToken))
            {
                return string.Empty;
            }

            try
            {
                var bytes = Convert.FromBase64String(settings.EncryptedCloudminiToken);
                var unprotectedBytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(unprotectedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void SaveCloudminiToken(AppSettings settings, string token, bool rememberToken)
        {
            if (settings == null)
            {
                return;
            }

            settings.RememberCloudminiToken = rememberToken;
            settings.EncryptedCloudminiToken = rememberToken && !string.IsNullOrWhiteSpace(token)
                ? Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(token.Trim()), null, DataProtectionScope.CurrentUser))
                : null;
        }

        public static string GetSettingsPath()
        {
            return Path.Combine(GetApplicationDataDirectory(), "settings.user.xml");
        }

        public static string GetApplicationDataDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RdpManager");
        }

        public static void EnsureApplicationDataDirectory()
        {
            var directory = GetApplicationDataDirectory();
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
