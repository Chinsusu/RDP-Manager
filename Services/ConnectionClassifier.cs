using System;
using System.Globalization;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class ConnectionClassifier
    {
        public static bool IsLinuxPort(string port)
        {
            return string.Equals((port ?? string.Empty).Trim(), "22", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetPlatformLabel(string port)
        {
            return IsLinuxPort(port) ? "Linux" : "Windows";
        }

        public static bool MatchesPlatform(string port, PlatformFilter filter)
        {
            if (filter == PlatformFilter.All)
            {
                return true;
            }

            var isLinux = IsLinuxPort(port);
            return filter == PlatformFilter.Linux ? isLinux : !isLinux;
        }

        public static bool MatchesLocalStatus(RdpEntry entry, StatusFilter filter)
        {
            if (filter == StatusFilter.All)
            {
                return true;
            }

            return GetLocalStatusFilter(entry) == filter;
        }

        public static bool MatchesRemoteStatus(CloudminiVps remote, StatusFilter filter)
        {
            if (filter == StatusFilter.All)
            {
                return true;
            }

            return GetRemoteStatusFilter(remote == null ? null : remote.Status) == filter;
        }

        public static string GetLocalStatusLabel(RdpEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (!entry.IsProviderManaged || string.IsNullOrWhiteSpace(entry.SourceStatus))
            {
                return "Manual";
            }

            return ToDisplayStatus(entry.SourceStatus);
        }

        public static StatusFilter GetLocalStatusFilter(RdpEntry entry)
        {
            if (entry == null)
            {
                return StatusFilter.Other;
            }

            if (!entry.IsProviderManaged || string.IsNullOrWhiteSpace(entry.SourceStatus))
            {
                return StatusFilter.Other;
            }

            return GetRemoteStatusFilter(entry.SourceStatus);
        }

        public static StatusFilter GetRemoteStatusFilter(string rawStatus)
        {
            switch (NormalizeStatus(rawStatus))
            {
                case "online":
                case "running":
                case "active":
                case "started":
                case "on":
                    return StatusFilter.Online;
                case "offline":
                case "stopped":
                case "inactive":
                case "suspended":
                case "disabled":
                case "off":
                    return StatusFilter.Offline;
                default:
                    return StatusFilter.Other;
            }
        }

        public static string GetPlatformFilterLabel(PlatformFilter filter)
        {
            switch (filter)
            {
                case PlatformFilter.Windows:
                    return "Windows";
                case PlatformFilter.Linux:
                    return "Linux";
                default:
                    return "All";
            }
        }

        public static string GetStatusFilterLabel(StatusFilter filter, bool localContext)
        {
            switch (filter)
            {
                case StatusFilter.Online:
                    return "Online";
                case StatusFilter.Offline:
                    return "Offline";
                case StatusFilter.Other:
                    return localContext ? "Manual/Other" : "Other";
                default:
                    return "All";
            }
        }

        public static string ToDisplayStatus(string rawStatus)
        {
            var normalized = NormalizeStatus(rawStatus);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "Other";
            }

            var spaced = normalized.Replace("_", " ").Replace("-", " ");
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
        }

        private static string NormalizeStatus(string rawStatus)
        {
            return (rawStatus ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
