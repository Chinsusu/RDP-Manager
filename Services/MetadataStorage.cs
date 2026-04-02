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
            var document = LoadDocument(GetMetadataPath(csvPath));
            var map = document.Entries.ToDictionary(entry => entry.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                EntryMetadata metadata;
                if (!map.TryGetValue(GetKey(entry), out metadata))
                {
                    entry.IsFavorite = false;
                    entry.LastConnectedUtc = null;
                    continue;
                }

                entry.IsFavorite = metadata.IsFavorite;
                entry.LastConnectedUtc = metadata.GetLastConnectedUtc();
            }
        }

        public static void Save(string csvPath, IEnumerable<RdpEntry> entries)
        {
            var path = GetMetadataPath(csvPath);
            var document = new MetadataDocument
            {
                Entries = entries
                    .Where(entry => entry.IsFavorite || entry.LastConnectedUtc.HasValue)
                    .Select(entry => new EntryMetadata
                    {
                        Key = GetKey(entry),
                        IsFavorite = entry.IsFavorite,
                        LastConnectedUtc = entry.LastConnectedUtc.HasValue
                            ? entry.LastConnectedUtc.Value.ToString("o")
                            : null
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

        public bool IsFavorite { get; set; }

        public string LastConnectedUtc { get; set; }

        public DateTime? GetLastConnectedUtc()
        {
            DateTime value;
            if (DateTime.TryParse(LastConnectedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out value))
            {
                return value;
            }

            return null;
        }
    }
}
