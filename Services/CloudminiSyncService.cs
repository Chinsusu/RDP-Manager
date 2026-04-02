using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class CloudminiSyncService
    {
        public const string ProviderName = "cloudmini";

        public static List<CloudminiSyncPreviewItem> BuildPreview(IEnumerable<RdpEntry> localEntries, IEnumerable<CloudminiVps> remoteItems, CloudminiSyncOptions options)
        {
            var entries = localEntries == null ? new List<RdpEntry>() : localEntries.ToList();
            var preview = new List<CloudminiSyncPreviewItem>();

            foreach (var remote in remoteItems ?? Enumerable.Empty<CloudminiVps>())
            {
                var matchedEntry = FindMatch(entries, remote);
                preview.Add(new CloudminiSyncPreviewItem
                {
                    Remote = remote,
                    MatchedEntry = matchedEntry,
                    SyncAction = ComputeAction(matchedEntry, remote, options),
                    Note = ComputeNote(matchedEntry, remote, options)
                });
            }

            return preview;
        }

        public static CloudminiSyncResult ApplySync(ObservableCollection<RdpEntry> localEntries, IEnumerable<CloudminiSyncPreviewItem> previewItems, CloudminiSyncOptions options)
        {
            var now = DateTime.UtcNow;
            var items = previewItems == null ? new List<CloudminiSyncPreviewItem>() : previewItems.ToList();
            var result = new CloudminiSyncResult
            {
                TotalCount = items.Count
            };

            foreach (var item in items)
            {
                if (!item.IsSelected)
                {
                    continue;
                }

                result.SelectedCount++;

                if (string.Equals(item.SyncAction, "Skip", StringComparison.OrdinalIgnoreCase))
                {
                    result.SkippedCount++;
                    continue;
                }

                if (string.Equals(item.SyncAction, "Conflict", StringComparison.OrdinalIgnoreCase))
                {
                    result.ConflictCount++;
                    continue;
                }

                if (item.Remote == null)
                {
                    result.SkippedCount++;
                    continue;
                }

                if (item.MatchedEntry == null)
                {
                    localEntries.Add(CreateEntry(item.Remote, options, now));
                    result.CreatedCount++;
                    continue;
                }

                UpdateEntry(item.MatchedEntry, item.Remote, options, now);
                result.UpdatedCount++;
            }

            return result;
        }

        public static string BuildDefaultHostName(CloudminiVps remote)
        {
            if (remote == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(remote.Ip)
                ? remote.Id.ToString()
                : string.Format("{0} - {1}", remote.Id, remote.Ip.Trim());
        }

        private static RdpEntry FindMatch(IList<RdpEntry> localEntries, CloudminiVps remote)
        {
            if (localEntries == null || remote == null)
            {
                return null;
            }

            var sourceId = remote.Id.ToString();
            foreach (var entry in localEntries)
            {
                if (string.Equals(entry.SourceProvider, ProviderName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            var normalizedPort = NormalizePort(remote.Port);
            foreach (var entry in localEntries)
            {
                if (string.Equals((entry.Host ?? string.Empty).Trim(), (remote.Ip ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(NormalizePort(entry.Port), normalizedPort, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((entry.User ?? string.Empty).Trim(), (remote.User ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private static string ComputeAction(RdpEntry matchedEntry, CloudminiVps remote, CloudminiSyncOptions options)
        {
            if (remote == null)
            {
                return "Skip";
            }

            if (options != null &&
                options.ImportOnlyOnline &&
                !string.Equals(remote.Status, "online", StringComparison.OrdinalIgnoreCase))
            {
                return "Skip";
            }

            if (matchedEntry == null)
            {
                return "New";
            }

            var hasChanges =
                !string.Equals((matchedEntry.Host ?? string.Empty).Trim(), (remote.Ip ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(NormalizePort(matchedEntry.Port), NormalizePort(remote.Port), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals((matchedEntry.User ?? string.Empty).Trim(), (remote.User ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) ||
                ((options == null || options.OverwritePasswordFromProvider) &&
                 !string.Equals(matchedEntry.Password ?? string.Empty, remote.Password ?? string.Empty, StringComparison.Ordinal)) ||
                !string.Equals(matchedEntry.SourceProvider ?? string.Empty, ProviderName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(matchedEntry.SourceId ?? string.Empty, remote.Id.ToString(), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(matchedEntry.SourceStatus ?? string.Empty, remote.Status ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(matchedEntry.SourceLocation ?? string.Empty, remote.Location ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                matchedEntry.SourceCreatedAtUtc != remote.CreatedAtUtc ||
                matchedEntry.SourceExpiredAtUtc != remote.ExpiredAtUtc ||
                !matchedEntry.IsProviderManaged ||
                ((options == null || !options.KeepLocalHostName) &&
                 !string.Equals(matchedEntry.HostName ?? string.Empty, BuildDefaultHostName(remote), StringComparison.Ordinal));

            return hasChanges ? "Update" : "Skip";
        }

        private static string ComputeNote(RdpEntry matchedEntry, CloudminiVps remote, CloudminiSyncOptions options)
        {
            if (remote == null)
            {
                return "Missing remote payload.";
            }

            if (options != null &&
                options.ImportOnlyOnline &&
                !string.Equals(remote.Status, "online", StringComparison.OrdinalIgnoreCase))
            {
                return "Skipped because Import only online is enabled.";
            }

            if (matchedEntry == null)
            {
                return "New VPS from Cloudmini.";
            }

            if (string.Equals(matchedEntry.SourceProvider, ProviderName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(matchedEntry.SourceId, remote.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return "Matched by provider source id.";
            }

            return "Matched by host, port, and user.";
        }

        private static RdpEntry CreateEntry(CloudminiVps remote, CloudminiSyncOptions options, DateTime now)
        {
            var entry = new RdpEntry();
            UpdateEntry(entry, remote, options, now);
            return entry;
        }

        private static void UpdateEntry(RdpEntry entry, CloudminiVps remote, CloudminiSyncOptions options, DateTime now)
        {
            entry.Host = (remote.Ip ?? string.Empty).Trim();
            entry.Port = NormalizePort(remote.Port);
            entry.User = (remote.User ?? string.Empty).Trim();

            if (options == null || options.OverwritePasswordFromProvider || string.IsNullOrWhiteSpace(entry.Password))
            {
                entry.Password = remote.Password ?? string.Empty;
            }

            if (options == null || !options.KeepLocalHostName || string.IsNullOrWhiteSpace(entry.HostName))
            {
                entry.HostName = BuildDefaultHostName(remote);
            }

            entry.SourceProvider = ProviderName;
            entry.SourceId = remote.Id.ToString();
            entry.SourceStatus = remote.Status;
            entry.SourceLocation = remote.Location;
            entry.SourceCreatedAtUtc = remote.CreatedAtUtc;
            entry.SourceExpiredAtUtc = remote.ExpiredAtUtc;
            entry.LastSyncedUtc = now;
            entry.IsProviderManaged = true;
        }

        private static string NormalizePort(string value)
        {
            int port;
            if (int.TryParse((value ?? string.Empty).Trim(), out port) && port > 0 && port <= 65535)
            {
                return port.ToString();
            }

            return "3389";
        }
    }
}
