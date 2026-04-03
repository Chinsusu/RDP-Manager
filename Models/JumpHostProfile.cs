using System.Xml.Serialization;

namespace RdpManager.Models
{
    public class JumpHostProfile
    {
        public JumpHostProfile()
        {
            Id = string.Empty;
            Name = string.Empty;
            Host = string.Empty;
            Port = 22;
            User = string.Empty;
            AuthMode = JumpHostAuthMode.EmbeddedPrivateKey;
            SecretRefId = string.Empty;
            PassphraseSecretRefId = string.Empty;
            ImportedKeyLabel = string.Empty;
            UseAgent = false;
            StrictHostKeyCheckingMode = "Ask";
            HostKeyFingerprint = string.Empty;
            ConnectTimeoutSeconds = 10;
            KeepAliveSeconds = 30;
            RuntimePassword = string.Empty;
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public string Host { get; set; }

        public int Port { get; set; }

        public string User { get; set; }

        public JumpHostAuthMode AuthMode { get; set; }

        public string SecretRefId { get; set; }

        public string PassphraseSecretRefId { get; set; }

        public string ImportedKeyLabel { get; set; }

        public bool UseAgent { get; set; }

        public string StrictHostKeyCheckingMode { get; set; }

        public string HostKeyFingerprint { get; set; }

        public int ConnectTimeoutSeconds { get; set; }

        public int KeepAliveSeconds { get; set; }

        [XmlIgnore]
        public string RuntimePassword { get; set; }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Name))
                {
                    return Name;
                }

                if (!string.IsNullOrWhiteSpace(Host))
                {
                    return Host;
                }

                return "New jump host";
            }
        }
    }
}
