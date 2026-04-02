namespace RdpManager.Models
{
    public class AppSettings
    {
        public AppSettings()
        {
            KeepLocalHostName = true;
            OverwritePasswordFromProvider = true;
            ImportOnlyOnline = false;
        }

        public bool RememberCloudminiToken { get; set; }

        public string EncryptedCloudminiToken { get; set; }

        public bool KeepLocalHostName { get; set; }

        public bool OverwritePasswordFromProvider { get; set; }

        public bool ImportOnlyOnline { get; set; }

        public string LastCloudminiSyncSummary { get; set; }

        public System.DateTime? LastCloudminiSyncUtc { get; set; }
    }
}
