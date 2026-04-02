using System;
using System.Collections.Generic;
using System.Linq;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class ConnectionListService
    {
        public static PagedResult<RdpEntry> BuildPage(
            IEnumerable<RdpEntry> entries,
            string query,
            NavigationFilter navigationFilter,
            PlatformFilter platformFilter,
            StatusFilter statusFilter,
            int requestedPage,
            int pageSize)
        {
            var filteredEntries = BuildFilteredEntries(entries, query, navigationFilter, platformFilter, statusFilter).ToList();
            var totalPages = Math.Max(1, (int)Math.Ceiling(filteredEntries.Count / (double)pageSize));
            var currentPage = Math.Max(1, Math.Min(requestedPage, totalPages));

            return new PagedResult<RdpEntry>
            {
                Items = filteredEntries
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList(),
                TotalCount = filteredEntries.Count,
                CurrentPage = currentPage,
                TotalPages = totalPages
            };
        }

        public static IList<RdpEntry> BuildFilteredEntries(
            IEnumerable<RdpEntry> entries,
            string query,
            NavigationFilter navigationFilter,
            PlatformFilter platformFilter,
            StatusFilter statusFilter)
        {
            var normalizedQuery = (query ?? string.Empty).Trim();
            var filteredEntries = (entries ?? Enumerable.Empty<RdpEntry>())
                .Where(entry =>
                    entry != null &&
                    MatchesNavigationFilter(entry, navigationFilter) &&
                    ConnectionClassifier.MatchesPlatform(entry.Port, platformFilter) &&
                    ConnectionClassifier.MatchesLocalStatus(entry, statusFilter) &&
                    MatchesQuery(entry, normalizedQuery));

            if (navigationFilter == NavigationFilter.Recent)
            {
                filteredEntries = filteredEntries.OrderByDescending(entry => entry.LastConnectedUtc ?? DateTime.MinValue);
            }

            return filteredEntries.ToList();
        }

        public static string GetEmptyStateMessage(NavigationFilter navigationFilter, PlatformFilter platformFilter, StatusFilter statusFilter)
        {
            var qualifier = BuildQualifier(platformFilter, statusFilter, true);
            switch (navigationFilter)
            {
                case NavigationFilter.Favorites:
                    return qualifier.Length == 0
                        ? "No favorite connections yet. Select an entry and click the star in Entry editor."
                        : string.Format("No {0}favorite connections match the current filter.", qualifier);
                case NavigationFilter.Recent:
                    return qualifier.Length == 0
                        ? "No recent connections yet. Launch an RDP session once and it will appear here."
                        : string.Format("No {0}recent connections match the current filter.", qualifier);
                default:
                    return qualifier.Length == 0
                        ? "No connections found. Add a new entry or open another CSV file."
                        : string.Format("No {0}connections match the current filter.", qualifier);
            }
        }

        private static bool MatchesNavigationFilter(RdpEntry entry, NavigationFilter navigationFilter)
        {
            switch (navigationFilter)
            {
                case NavigationFilter.Favorites:
                    return entry.IsFavorite;
                case NavigationFilter.Recent:
                    return entry.LastConnectedUtc.HasValue;
                default:
                    return true;
            }
        }

        private static bool MatchesQuery(RdpEntry entry, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return ContainsIgnoreCase(entry.HostName, query) ||
                   ContainsIgnoreCase(entry.Host, query) ||
                   ContainsIgnoreCase(entry.User, query);
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildQualifier(PlatformFilter platformFilter, StatusFilter statusFilter, bool localContext)
        {
            var parts = new List<string>();
            if (statusFilter != StatusFilter.All)
            {
                parts.Add(ConnectionClassifier.GetStatusFilterLabel(statusFilter, localContext).ToLowerInvariant());
            }

            if (platformFilter != PlatformFilter.All)
            {
                parts.Add(ConnectionClassifier.GetPlatformFilterLabel(platformFilter).ToLowerInvariant());
            }

            return parts.Count == 0 ? string.Empty : string.Join(" ", parts) + " ";
        }
    }
}
