using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class SecretVault
    {
        public static string SaveSecret(string existingSecretId, SecretKind kind, string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return string.Empty;
            }

            var secretId = string.IsNullOrWhiteSpace(existingSecretId)
                ? Guid.NewGuid().ToString("N")
                : existingSecretId.Trim();

            var document = LoadDocument();
            var existing = document.Secrets.FirstOrDefault(secret => string.Equals(secret.SecretId, secretId, StringComparison.OrdinalIgnoreCase));
            var cipherText = Protect(plainText);

            if (existing == null)
            {
                document.Secrets.Add(new SecretRecord
                {
                    SecretId = secretId,
                    Kind = kind,
                    CipherText = cipherText,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                });
            }
            else
            {
                existing.Kind = kind;
                existing.CipherText = cipherText;
                existing.UpdatedUtc = DateTime.UtcNow;
            }

            SaveDocument(document);
            return secretId;
        }

        public static string LoadSecret(string secretId)
        {
            if (string.IsNullOrWhiteSpace(secretId))
            {
                return string.Empty;
            }

            var document = LoadDocument();
            var record = document.Secrets.FirstOrDefault(secret => string.Equals(secret.SecretId, secretId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (record == null || string.IsNullOrWhiteSpace(record.CipherText))
            {
                return string.Empty;
            }

            try
            {
                return Unprotect(record.CipherText);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool HasSecret(string secretId)
        {
            if (string.IsNullOrWhiteSpace(secretId))
            {
                return false;
            }

            var document = LoadDocument();
            return document.Secrets.Any(secret => string.Equals(secret.SecretId, secretId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static SecretKind? GetSecretKind(string secretId)
        {
            if (string.IsNullOrWhiteSpace(secretId))
            {
                return null;
            }

            var document = LoadDocument();
            var record = document.Secrets.FirstOrDefault(secret => string.Equals(secret.SecretId, secretId.Trim(), StringComparison.OrdinalIgnoreCase));
            return record == null ? (SecretKind?)null : record.Kind;
        }

        public static void DeleteSecret(string secretId)
        {
            if (string.IsNullOrWhiteSpace(secretId))
            {
                return;
            }

            var document = LoadDocument();
            var removed = document.Secrets.RemoveAll(secret => string.Equals(secret.SecretId, secretId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                SaveDocument(document);
            }
        }

        public static string GetSecretsPath()
        {
            return Path.Combine(SettingsStorage.GetApplicationDataDirectory(), "secrets.user.xml");
        }

        private static SecretVaultDocument LoadDocument()
        {
            var path = GetSecretsPath();
            if (!File.Exists(path))
            {
                return new SecretVaultDocument();
            }

            try
            {
                var serializer = new XmlSerializer(typeof(SecretVaultDocument));
                using (var stream = File.OpenRead(path))
                {
                    var document = serializer.Deserialize(stream) as SecretVaultDocument;
                    return document ?? new SecretVaultDocument();
                }
            }
            catch
            {
                return new SecretVaultDocument();
            }
        }

        private static void SaveDocument(SecretVaultDocument document)
        {
            SettingsStorage.EnsureApplicationDataDirectory();

            var serializer = new XmlSerializer(typeof(SecretVaultDocument));
            using (var stream = File.Create(GetSecretsPath()))
            {
                serializer.Serialize(stream, document ?? new SecretVaultDocument());
            }
        }

        private static string Protect(string plainText)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static string Unprotect(string cipherText)
        {
            var protectedBytes = Convert.FromBase64String(cipherText);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }

    [Serializable]
    public class SecretVaultDocument
    {
        public SecretVaultDocument()
        {
            Secrets = new List<SecretRecord>();
        }

        public List<SecretRecord> Secrets { get; set; }
    }
}
