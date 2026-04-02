namespace RdpManager.Models
{
    public class CloudminiSyncResult
    {
        public int CreatedCount { get; set; }

        public int UpdatedCount { get; set; }

        public int SkippedCount { get; set; }

        public int ConflictCount { get; set; }

        public int SelectedCount { get; set; }

        public int TotalCount { get; set; }
    }
}
