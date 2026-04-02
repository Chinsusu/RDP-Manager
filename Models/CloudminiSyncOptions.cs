namespace RdpManager.Models
{
    public class CloudminiSyncOptions
    {
        public bool KeepLocalHostName { get; set; }

        public bool OverwritePasswordFromProvider { get; set; }

        public bool ImportOnlyOnline { get; set; }
    }
}
