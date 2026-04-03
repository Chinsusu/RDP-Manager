using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class MetadataStorage
    {
        public static void Apply(string csvPath, IEnumerable<RdpEntry> entries)
        {
            if (SqliteStorage.GetDatabasePath().Equals(csvPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(csvPath), ".db", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var document = LoadDocument(GetMetadataPath(csvPath));
            var map = document.Entries.ToDictionary(entry => entry.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                EntryMetadata metadata;
                if (!map.TryGetValue(GetKey(entry), out metadata))
                {
                    entry.GroupName = null;
                    entry.Tags = null;
                    entry.Notes = null;
                    entry.TransportMode = TransportMode.Direct;
                    entry.JumpHostProfileId = null;
                    entry.TunnelTargetHostOverride = null;
                    entry.TunnelTargetPortOverride = null;
                    entry.IsFavorite = false;
                    entry.LastConnectedUtc = null;
                    entry.HealthStatus = null;
                    entry.LastHealthCheckedUtc = null;
                    entry.SourceProvider = null;
                    entry.SourceId = null;
                    entry.SourceStatus = null;
                    entry.SourceLocation = null;
                    entry.SourceCreatedAtUtc = null;
                    entry.SourceExpiredAtUtc = null;
                    entry.LastSyncedUtc = null;
                    entry.IsProviderManaged = false;
                    continue;
                }

                entry.GroupName = metadata.GroupName;
                entry.Tags = metadata.Tags;
                entry.Notes = metadata.Notes;
                entry.TransportMode = metadata.GetTransportMode();
                entry.JumpHostProfileId = metadata.JumpHostProfileId;
                entry.TunnelTargetHostOverride = metadata.TunnelTargetHostOverride;
                entry.TunnelTargetPortOverride = metadata.TunnelTargetPortOverride;
                entry.IsFavorite = metadata.IsFavorite;
                entry.LastConnectedUtc = metadata.GetLastConnectedUtc();
                entry.HealthStatus = metadata.HealthStatus;
                entry.LastHealthCheckedUtc = metadata.GetLastHealthCheckedUtc();
                entry.SourceProvider = metadata.SourceProvider;
                entry.SourceId = metadata.SourceId;
                entry.SourceStatus = metadata.SourceStatus;
                entry.SourceLocation = metadata.SourceLocation;
                entry.SourceCreatedAtUtc = metadata.GetSourceCreatedAtUtc();
                entry.SourceExpiredAtUtc = metadata.GetSourceExpiredAtUtc();
                entry.LastSyncedUtc = metadata.GetLastSyncedUtc();
                entry.IsProviderManaged = metadata.IsProviderManaged;
            }
        }

        public static void Save(string csvPath, IEnumerable<RdpEntry> entries)
        {
            if (SqliteStorage.GetDatabasePath().Equals(csvPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(csvPath), ".db", StringComparison.OrdinalIgnoreCase))
            {
                SqliteStorage.SaveConnections(csvPath, entries);
                return;
            }

            var path = GetMetadataPath(csvPath);
            var document = new MetadataDocument
            {
                Entries = entries
                    .Where(entry =>
                        !string.IsNullOrWhiteSpace(entry.GroupName) ||
                        !string.IsNullOrWhiteSpace(entry.Tags) ||
                        !string.IsNullOrWhiteSpace(entry.Notes) ||
                        entry.TransportMode != TransportMode.Direct ||
                        !string.IsNullOrWhiteSpace(entry.JumpHostProfileId) ||
                        !string.IsNullOrWhiteSpace(entry.TunnelTargetHostOverride) ||
                        !string.IsNullOrWhiteSpace(entry.TunnelTargetPortOverride) ||
                        entry.IsFavorite ||
                        entry.LastConnectedUtc.HasValue ||
                        !string.IsNullOrWhiteSpace(entry.HealthStatus) ||
                        entry.LastHealthCheckedUtc.HasValue ||
                        entry.IsProviderManaged ||
                        !string.IsNullOrWhiteSpace(entry.SourceProvider) ||
                        !string.IsNullOrWhiteSpace(entry.SourceId) ||
                        entry.LastSyncedUtc.HasValue)
                    .Select(entry => new EntryMetadata
                    {
                        Key = GetKey(entry),
                        GroupName = entry.GroupName,
                        Tags = entry.Tags,
                        Notes = entry.Notes,
                        TransportMode = entry.TransportMode.ToString(),
                        JumpHostProfileId = entry.JumpHostProfileId,
                        TunnelTargetHostOverride = entry.TunnelTargetHostOverride,
                        TunnelTargetPortOverride = entry.TunnelTargetPortOverride,
                        IsFavorite = entry.IsFavorite,
                        LastConnectedUtc = entry.LastConnectedUtc.HasValue
                            ? entry.LastConnectedUtc.Value.ToString("o")
                            : null,
                        HealthStatus = entry.HealthStatus,
                        LastHealthCheckedUtc = entry.LastHealthCheckedUtc.HasValue
                            ? entry.LastHealthCheckedUtc.Value.ToString("o")
                            : null,
                        SourceProvider = entry.SourceProvider,
                        SourceId = entry.SourceId,
                        SourceStatus = entry.SourceStatus,
                        SourceLocation = entry.SourceLocation,
                        SourceCreatedAtUtc = entry.SourceCreatedAtUtc.HasValue ? entry.SourceCreatedAtUtc.Value.ToString("o") : null,
                        SourceExpiredAtUtc = entry.SourceExpiredAtUtc.HasValue ? entry.SourceExpiredAtUtc.Value.ToString("o") : null,
                        LastSyncedUtc = entry.LastSyncedUtc.HasValue ? entry.LastSyncedUtc.Value.ToString("o") : null,
                        IsProviderManaged = entry.IsProviderManaged
                    })
                    .ToList()
            };

            var serializer = new XmlSerializer(typeof(MetadataDocument));
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var stream = File.Create(path))
            {
                serializer.Serialize(stream, document);
            }
        }

        private static MetadataDocument LoadDocument(string path)
        {
            if (!File.Exists(path))
            {
                return new MetadataDocument();
            }

            try
            {
                var serializer = new XmlSerializer(typeof(MetadataDocument));
                using (var stream = File.OpenRead(path))
                {
                    var document = serializer.Deserialize(stream) as MetadataDocument;
                    return document ?? new MetadataDocument();
                }
            }
            catch
            {
                return new MetadataDocument();
            }
        }

        private static string GetMetadataPath(string csvPath)
        {
            var directory = Path.GetDirectoryName(csvPath) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(csvPath) ?? "clients";
            return Path.Combine(directory, name + ".meta.xml");
        }

        private static string GetKey(RdpEntry entry)
        {
            return string.Format(
                "{0}|{1}|{2}",
                Normalize(entry.Host),
                Normalize(entry.Port),
                Normalize(entry.User));
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }

    [Serializable]
    public class MetadataDocument
    {
        public MetadataDocument()
        {
            Entries = new List<EntryMetadata>();
        }

        public List<EntryMetadata> Entries { get; set; }
    }

    public class EntryMetadata
    {
        public string Key { get; set; }

        public string GroupName { get; set; }

        public string Tags { get; set; }

        public string Notes { get; set; }

        public string TransportMode { get; set; }

        public string JumpHostProfileId { get; set; }

        public string TunnelTargetHostOverride { get; set; }

        public string TunnelTargetPortOverride { get; set; }

        public bool IsFavorite { get; set; }

        public string LastConnectedUtc { get; set; }

        public string HealthStatus { get; set; }

        public string LastHealthCheckedUtc { get; set; }

        public string SourceProvider { get; set; }

        public string SourceId { get; set; }

        public string SourceStatus { get; set; }

        public string SourceLocation { get; set; }

        public string SourceCreatedAtUtc { get; set; }

        public string SourceExpiredAtUtc { get; set; }

        public string LastSyncedUtc { get; set; }

        public bool IsProviderManaged { get; set; }

        public DateTime? GetLastConnectedUtc()
        {
            DateTime value;
            if (DateTime.TryParse(LastConnectedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out value))
            {
                return value;
            }

            return null;
        }

        public DateTime? GetSourceCreatedAtUtc()
        {
            return ParseDate(SourceCreatedAtUtc);
        }

        public DateTime? GetLastHealthCheckedUtc()
        {
            return ParseDate(LastHealthCheckedUtc);
        }

        public DateTime? GetSourceExpiredAtUtc()
        {
            return ParseDate(SourceExpiredAtUtc);
        }

        public DateTime? GetLastSyncedUtc()
        {
            return ParseDate(LastSyncedUtc);
        }

        public TransportMode GetTransportMode()
        {
            TransportMode parsed;
            if (Enum.TryParse(TransportMode, true, out parsed))
            {
                return parsed;
            }

            return Models.TransportMode.Direct;
        }

        private static DateTime? ParseDate(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out parsed))
            {
                return parsed;
            }

            return null;
        }
    }
}
