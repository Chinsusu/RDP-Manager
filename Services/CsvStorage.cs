using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class CsvStorage
    {
        private static readonly string[] Header = { "HostName", "Host", "Port", "User", "Password" };

        public static ObservableCollection<RdpEntry> Load(string path)
        {
            var result = new ObservableCollection<RdpEntry>();
            var hasHostNameColumn = false;
            EnsureFileExists(path);

            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var fields = ParseLine(line).ToList();
                if (fields.Count == 0)
                {
                    continue;
                }

                if (IsHeaderRow(fields))
                {
                    hasHostNameColumn = HasHostNameHeader(fields);
                    continue;
                }

                var originalFieldCount = fields.Count;
                while (fields.Count < 5)
                {
                    fields.Add(string.Empty);
                }

                if (hasHostNameColumn || originalFieldCount > 4)
                {
                    result.Add(new RdpEntry
                    {
                        HostName = fields[0].Trim(),
                        Host = fields[1].Trim(),
                        Port = string.IsNullOrWhiteSpace(fields[2]) ? "3389" : fields[2].Trim(),
                        User = fields[3],
                        Password = fields[4]
                    });

                    continue;
                }

                result.Add(new RdpEntry
                {
                    Host = fields[0].Trim(),
                    Port = string.IsNullOrWhiteSpace(fields[1]) ? "3389" : fields[1].Trim(),
                    User = fields[2],
                    Password = fields[3]
                });
            }

            return result;
        }

        public static void Save(IEnumerable<RdpEntry> entries, string path)
        {
            EnsureParentDirectory(path);

            var lines = new List<string> { string.Join(",", Header) };
            lines.AddRange(entries.Select(entry => string.Join(",",
                Escape(entry.HostName),
                Escape(entry.Host),
                Escape(string.IsNullOrWhiteSpace(entry.Port) ? "3389" : entry.Port),
                Escape(entry.User),
                Escape(entry.Password))));

            File.WriteAllLines(path, lines, new UTF8Encoding(false));
        }

        public static void EnsureFileExists(string path)
        {
            EnsureParentDirectory(path);

            if (!File.Exists(path))
            {
                File.WriteAllText(path, string.Join(",", Header) + "\r\n", new UTF8Encoding(false));
            }
        }

        private static void EnsureParentDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static bool IsHeaderRow(IList<string> fields)
        {
            if (fields.Count < 4)
            {
                return false;
            }

            if (HasHostNameHeader(fields))
            {
                return fields.Count >= 5 &&
                       string.Equals(fields[1], "Host", System.StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(fields[2], "Port", System.StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(fields[3], "User", System.StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(fields[4], "Password", System.StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(fields[0], "Host", System.StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(fields[1], "Port", System.StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(fields[2], "User", System.StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(fields[3], "Password", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasHostNameHeader(IList<string> fields)
        {
            return fields.Count >= 1 &&
                   string.Equals(fields[0], "HostName", System.StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> ParseLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var index = 0; index < line.Length; index++)
            {
                var character = line[index];

                if (inQuotes)
                {
                    if (character == '"')
                    {
                        var nextIsQuote = index + 1 < line.Length && line[index + 1] == '"';
                        if (nextIsQuote)
                        {
                            current.Append('"');
                            index++;
                            continue;
                        }

                        inQuotes = false;
                        continue;
                    }

                    current.Append(character);
                    continue;
                }

                if (character == '"')
                {
                    inQuotes = true;
                    continue;
                }

                if (character == ',')
                {
                    values.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(character);
            }

            values.Add(current.ToString());
            return values;
        }

        private static string Escape(string value)
        {
            var safeValue = value ?? string.Empty;

            if (!safeValue.Contains(",") && !safeValue.Contains("\"") && !safeValue.Contains("\r") && !safeValue.Contains("\n"))
            {
                return safeValue;
            }

            return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
        }
    }
}
