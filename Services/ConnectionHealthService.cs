using System;
using System.Collections.Generic;
using System.Net.Sockets;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class ConnectionHealthService
    {
        public static string CheckAndApply(RdpEntry entry, int timeoutMilliseconds)
        {
            if (entry == null)
            {
                return null;
            }

            var status = Check(entry.Host, entry.Port, timeoutMilliseconds);
            entry.HealthStatus = status;
            entry.LastHealthCheckedUtc = DateTime.UtcNow;
            return status;
        }

        public static string Check(string host, string port, int timeoutMilliseconds)
        {
            int parsedPort;
            if (string.IsNullOrWhiteSpace(host))
            {
                return "invalid";
            }

            if (!int.TryParse((port ?? string.Empty).Trim(), out parsedPort) || parsedPort <= 0 || parsedPort > 65535)
            {
                parsedPort = 3389;
            }

            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(host.Trim(), parsedPort, null, null);
                    try
                    {
                        if (!result.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
                        {
                            return "timeout";
                        }

                        client.EndConnect(result);
                        return "reachable";
                    }
                    finally
                    {
                        result.AsyncWaitHandle.Close();
                    }
                }
            }
            catch (SocketException ex)
            {
                switch (ex.SocketErrorCode)
                {
                    case SocketError.ConnectionRefused:
                        return "refused";
                    case SocketError.TimedOut:
                        return "timeout";
                    case SocketError.HostNotFound:
                    case SocketError.NoData:
                        return "unresolved";
                    default:
                        return "error";
                }
            }
            catch
            {
                return "error";
            }
        }

        public static string GetDisplayLabel(string status)
        {
            switch ((status ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "reachable":
                    return "Reachable";
                case "timeout":
                    return "Timeout";
                case "refused":
                    return "Refused";
                case "unresolved":
                    return "DNS";
                case "invalid":
                    return "Invalid";
                case "error":
                    return "Error";
                default:
                    return "Unchecked";
            }
        }

        public static string GetDetails(string status, DateTime? checkedUtc)
        {
            var label = GetDisplayLabel(status);
            if (!checkedUtc.HasValue)
            {
                return label;
            }

            return string.Format("{0} at {1}", label, checkedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        }

        public static IDictionary<string, int> SummarizeStatuses(IEnumerable<string> statuses)
        {
            var summary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var status in statuses)
            {
                var key = GetDisplayLabel(status);
                if (!summary.ContainsKey(key))
                {
                    summary[key] = 0;
                }

                summary[key]++;
            }

            return summary;
        }
    }
}
