using System;

namespace RdpManager.Models
{
    public class SecretRecord
    {
        public SecretRecord()
        {
            SecretId = string.Empty;
            Kind = SecretKind.SshPrivateKey;
            CipherText = string.Empty;
            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = DateTime.UtcNow;
        }

        public string SecretId { get; set; }

        public SecretKind Kind { get; set; }

        public string CipherText { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime UpdatedUtc { get; set; }
    }
}
