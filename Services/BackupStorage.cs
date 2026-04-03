using System;
using System.Collections.Generic;
using System.IO;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class BackupStorage
    {
        public static string CreatePreSyncBackup(string csvPath, IEnumerable<RdpEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(csvPath))
            {
                throw new InvalidOperationException("Current CSV path is not set.");
            }

            var directory = Path.GetDirectoryName(csvPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = AppDomain.CurrentDomain.BaseDirectory;
            }

            var backupDirectory = Path.Combine(directory, "backups");
            Directory.CreateDirectory(backupDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var fileName = Path.GetFileNameWithoutExtension(csvPath);
            var extension = Path.GetExtension(csvPath);
            if (string.Equals(extension, ".db", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".csv";
            }

            var backupPath = Path.Combine(backupDirectory, string.Format("{0}.pre-sync-{1}{2}", fileName, timestamp, extension));

            CsvStorage.Save(entries, backupPath);
            MetadataStorage.Save(backupPath, entries);
            return backupPath;
        }
    }
}
