using System.Collections.Generic;
using System.Linq;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class CloudminiFilterService
    {
        public static IList<CloudminiVps> Filter(IEnumerable<CloudminiVps> remoteItems, PlatformFilter platformFilter, StatusFilter statusFilter)
        {
            return (remoteItems ?? Enumerable.Empty<CloudminiVps>())
                .Where(remote =>
                    remote != null &&
                    ConnectionClassifier.MatchesPlatform(remote.Port, remote.User, platformFilter) &&
                    ConnectionClassifier.MatchesRemoteStatus(remote, statusFilter))
                .ToList();
        }

        public static string GetEmptyStateMessage(PlatformFilter platformFilter, StatusFilter statusFilter)
        {
            if (platformFilter == PlatformFilter.All && statusFilter == StatusFilter.All)
            {
                return "No Cloudmini VPS loaded yet.";
            }

            var parts = new List<string>();
            if (statusFilter != StatusFilter.All)
            {
                parts.Add(ConnectionClassifier.GetStatusFilterLabel(statusFilter, false).ToLowerInvariant());
            }

            if (platformFilter != PlatformFilter.All)
            {
                parts.Add(ConnectionClassifier.GetPlatformFilterLabel(platformFilter).ToLowerInvariant());
            }

            var qualifier = parts.Count == 0 ? string.Empty : string.Join(" ", parts) + " ";
            return string.Format("No {0}Cloudmini VPS match the current filter.", qualifier);
        }
    }
}
