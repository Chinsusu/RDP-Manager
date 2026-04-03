using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class JumpHostProfileStorage
    {
        public static List<JumpHostProfile> Load()
        {
            return SqliteStorage.LoadProxyProfiles(SqliteStorage.GetDatabasePath());
        }

        public static List<JumpHostProfile> LoadLegacyXml(string path)
        {
            if (!File.Exists(path))
            {
                return new List<JumpHostProfile>();
            }

            try
            {
                var serializer = new XmlSerializer(typeof(JumpHostProfileDocument));
                using (var stream = File.OpenRead(path))
                {
                    var document = serializer.Deserialize(stream) as JumpHostProfileDocument;
                    return NormalizeProfiles(document == null ? new List<JumpHostProfile>() : document.Profiles);
                }
            }
            catch
            {
                return new List<JumpHostProfile>();
            }
        }

        public static void Save(IEnumerable<JumpHostProfile> profiles)
        {
            SqliteStorage.SaveProxyProfiles(SqliteStorage.GetDatabasePath(), profiles);
        }

        public static string GetProfilesPath()
        {
            return SqliteStorage.GetDatabasePath();
        }

        public static string GetLegacyProfilesPath()
        {
            return Path.Combine(SettingsStorage.GetApplicationDataDirectory(), "jump-hosts.user.xml");
        }

        public static List<JumpHostProfile> NormalizeProfiles(IEnumerable<JumpHostProfile> profiles)
        {
            return (profiles == null ? new List<JumpHostProfile>() : profiles.ToList())
                .Where(profile => profile != null)
                .Select(profile => new JumpHostProfile
                {
                    Id = string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("N") : profile.Id.Trim(),
                    Name = (profile.Name ?? string.Empty).Trim(),
                    Host = (profile.Host ?? string.Empty).Trim(),
                    Port = profile.Port > 0 ? profile.Port : 22,
                    User = (profile.User ?? string.Empty).Trim(),
                    AuthMode = Enum.IsDefined(typeof(JumpHostAuthMode), profile.AuthMode)
                        ? profile.AuthMode
                        : JumpHostAuthMode.EmbeddedPrivateKey,
                    SecretRefId = (profile.SecretRefId ?? string.Empty).Trim(),
                    PassphraseSecretRefId = (profile.PassphraseSecretRefId ?? string.Empty).Trim(),
                    ImportedKeyLabel = (profile.ImportedKeyLabel ?? string.Empty).Trim(),
                    UseAgent = profile.UseAgent || profile.AuthMode == JumpHostAuthMode.Agent,
                    StrictHostKeyCheckingMode = string.IsNullOrWhiteSpace(profile.StrictHostKeyCheckingMode)
                        ? "Ask"
                        : profile.StrictHostKeyCheckingMode.Trim(),
                    HostKeyFingerprint = (profile.HostKeyFingerprint ?? string.Empty).Trim(),
                    ConnectTimeoutSeconds = profile.ConnectTimeoutSeconds > 0 ? profile.ConnectTimeoutSeconds : 10,
                    KeepAliveSeconds = profile.KeepAliveSeconds > 0 ? profile.KeepAliveSeconds : 30
                })
                .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    [Serializable]
    public class JumpHostProfileDocument
    {
        public JumpHostProfileDocument()
        {
            Profiles = new List<JumpHostProfile>();
        }

        public List<JumpHostProfile> Profiles { get; set; }
    }
}
