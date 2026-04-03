using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class RdpLauncher
    {
        private static readonly string TempDirectory = Path.Combine(Path.GetTempPath(), "RdpManager");

        public static Process Launch(RdpEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException("entry");
            }

            var host = (entry.Host ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new InvalidOperationException("Host cannot be empty.");
            }

            var port = ParsePort(entry.Port);
            var user = (entry.User ?? string.Empty).Trim();
            var password = entry.Password ?? string.Empty;

            return LaunchToEndpoint(host, port, user, password);
        }

        public static Process LaunchToEndpoint(string host, int port, string user, string password)
        {
            host = (host ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new InvalidOperationException("Host cannot be empty.");
            }

            if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
            {
                StoreCredentials(host, port, user, password);
            }

            var rdpFile = CreateTemporaryRdpFile(host, port, user);

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "mstsc.exe",
                Arguments = QuoteArgument(rdpFile),
                UseShellExecute = true
            });
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start mstsc.exe.");
            }

            return process;
        }

        public static void CleanupTemporaryFiles()
        {
            if (!Directory.Exists(TempDirectory))
            {
                return;
            }

            var cutoff = DateTime.Now.AddDays(-1);
            foreach (var file in Directory.GetFiles(TempDirectory, "*.rdp").Where(file => File.GetLastWriteTime(file) < cutoff))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }

        private static void StoreCredentials(string host, int port, string user, string password)
        {
            RunCmdKey("TERMSRV/" + host, user, password);

            if (port != 3389)
            {
                RunCmdKey("TERMSRV/" + host + ":" + port, user, password);
            }
        }

        private static void RunCmdKey(string target, string user, string password)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmdkey.exe",
                Arguments =
                    "/generic:" + QuoteArgument(target) +
                    " /user:" + QuoteArgument(user) +
                    " /pass:" + QuoteArgument(password),
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start cmdkey.exe.");
                }

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var error = process.StandardError.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(error))
                    {
                        error = process.StandardOutput.ReadToEnd();
                    }

                    throw new InvalidOperationException("cmdkey.exe failed. " + error.Trim());
                }
            }
        }

        private static string CreateTemporaryRdpFile(string host, int port, string user)
        {
            Directory.CreateDirectory(TempDirectory);

            var rdpFilePath = Path.Combine(TempDirectory, "session-" + Guid.NewGuid().ToString("N") + ".rdp");
            var address = port == 3389 ? host : host + ":" + port;
            var builder = new StringBuilder();

            builder.AppendLine("screen mode id:i:2");
            builder.AppendLine("use multimon:i:0");
            builder.AppendLine("desktopwidth:i:1366");
            builder.AppendLine("desktopheight:i:768");
            builder.AppendLine("session bpp:i:32");
            builder.AppendLine("compression:i:1");
            builder.AppendLine("prompt for credentials:i:0");
            builder.AppendLine("promptcredentialonce:i:1");
            builder.AppendLine("authentication level:i:2");
            builder.AppendLine("enablecredsspsupport:i:1");
            builder.AppendLine("full address:s:" + address);

            if (!string.IsNullOrWhiteSpace(user))
            {
                builder.AppendLine("username:s:" + user);
            }

            File.WriteAllText(rdpFilePath, builder.ToString(), new UTF8Encoding(false));
            return rdpFilePath;
        }

        public static int ParsePort(string rawPort)
        {
            int port;
            if (int.TryParse((rawPort ?? string.Empty).Trim(), out port) && port > 0 && port <= 65535)
            {
                return port;
            }

            return 3389;
        }

        private static string QuoteArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            if (!argument.Any(character => char.IsWhiteSpace(character) || character == '"'))
            {
                return argument;
            }

            var builder = new StringBuilder();
            var backslashCount = 0;

            builder.Append('"');

            foreach (var character in argument)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', backslashCount * 2 + 1);
                    builder.Append('"');
                    backslashCount = 0;
                    continue;
                }

                if (backslashCount > 0)
                {
                    builder.Append('\\', backslashCount);
                    backslashCount = 0;
                }

                builder.Append(character);
            }

            if (backslashCount > 0)
            {
                builder.Append('\\', backslashCount * 2);
            }

            builder.Append('"');
            return builder.ToString();
        }
    }
}
