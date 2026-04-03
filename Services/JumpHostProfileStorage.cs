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
            var path = GetProfilesPath();
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
                    return Normalize(document == null ? new List<JumpHostProfile>() : document.Profiles);
                }
            }
            catch
            {
                return new List<JumpHostProfile>();
            }
        }

        public static void Save(IEnumerable<JumpHostProfile> profiles)
        {
            SettingsStorage.EnsureApplicationDataDirectory();

            var document = new JumpHostProfileDocument
            {
                Profiles = Normalize(profiles == null ? new List<JumpHostProfile>() : profiles.ToList())
            };

            var serializer = new XmlSerializer(typeof(JumpHostProfileDocument));
            using (var stream = File.Create(GetProfilesPath()))
            {
                serializer.Serialize(stream, document);
            }
        }

        public static string GetProfilesPath()
        {
            return Path.Combine(SettingsStorage.GetApplicationDataDirectory(), "jump-hosts.user.xml");
        }

        private static List<JumpHostProfile> Normalize(IList<JumpHostProfile> profiles)
        {
            return (profiles ?? new List<JumpHostProfile>())
                .Where(profile => profile != null)
                .Select(profile => new JumpHostProfile
                {
                    Id = string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("N") : profile.Id.Trim(),
                    Name = (profile.Name ?? string.Empty).Trim(),
                    Host = (profile.Host ?? string.Empty).Trim(),
                    Port = profile.Port > 0 ? profile.Port : 22,
                    User = (profile.User ?? string.Empty).Trim(),
                    AuthMode = profile.AuthMode,
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
