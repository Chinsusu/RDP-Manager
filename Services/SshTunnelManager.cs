using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Renci.SshNet;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class SshTunnelManager
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<ActiveTunnelSession> ActiveSessions = new List<ActiveTunnelSession>();
        private static readonly string KnownHostsDirectory = Path.Combine(SettingsStorage.GetApplicationDataDirectory(), "ssh");
        private static readonly string KnownHostsFilePath = Path.Combine(KnownHostsDirectory, "known_hosts");

        public static void Launch(RdpEntry entry, JumpHostProfile profile)
        {
            if (entry == null)
            {
                throw new ArgumentNullException("entry");
            }

            if (profile == null)
            {
                throw new InvalidOperationException("Jump host profile not found.");
            }

            if (profile.AuthMode == JumpHostAuthMode.Password)
            {
                LaunchWithPasswordTunnel(entry, profile);
                return;
            }

            var targetHost = string.IsNullOrWhiteSpace(entry.TunnelTargetHostOverride)
                ? (entry.Host ?? string.Empty).Trim()
                : entry.TunnelTargetHostOverride.Trim();
            if (string.IsNullOrWhiteSpace(targetHost))
            {
                throw new InvalidOperationException("Tunnel target host cannot be empty.");
            }

            var targetPort = ParsePort(string.IsNullOrWhiteSpace(entry.TunnelTargetPortOverride) ? entry.Port : entry.TunnelTargetPortOverride);
            var localPort = FindAvailableLocalPort();
            var tempKeyFilePath = string.Empty;
            var sshProcess = null as Process;

            try
            {
                SettingsStorage.EnsureApplicationDataDirectory();
                Directory.CreateDirectory(KnownHostsDirectory);
                EnsureKnownHostsFileExists();

                if (profile.AuthMode == JumpHostAuthMode.EmbeddedPrivateKey)
                {
                    var privateKeyContent = SecretVault.LoadSecret(profile.SecretRefId);
                    if (string.IsNullOrWhiteSpace(privateKeyContent))
                    {
                        throw new InvalidOperationException("No SSH private key is stored for the selected jump host profile.");
                    }

                    tempKeyFilePath = TempKeyMaterializer.MaterializePrivateKey(privateKeyContent);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = ResolveSshPath(),
                    Arguments = BuildArguments(profile, targetHost, targetPort, localPort, tempKeyFilePath),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                sshProcess = Process.Start(startInfo);
                if (sshProcess == null)
                {
                    throw new InvalidOperationException("Failed to start ssh.exe.");
                }

                WaitForTunnelReady(sshProcess, localPort, profile.ConnectTimeoutSeconds);

                var mstscProcess = RdpLauncher.LaunchToEndpoint("127.0.0.1", localPort, entry.User, entry.Password, true);
                RegisterSession(new ActiveTunnelSession
                {
                    SshProcess = sshProcess,
                    MstscProcess = mstscProcess,
                    TempKeyFilePath = tempKeyFilePath
                });
            }
            catch
            {
                CleanupProcess(sshProcess);
                TempKeyMaterializer.Delete(tempKeyFilePath);
                throw;
            }
        }

        public static string TestProfile(JumpHostProfile profile)
        {
            if (profile == null)
            {
                throw new InvalidOperationException("Jump host profile not found.");
            }

            if (string.IsNullOrWhiteSpace((profile.Host ?? string.Empty).Trim()))
            {
                throw new InvalidOperationException("Jump host cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace((profile.User ?? string.Empty).Trim()))
            {
                throw new InvalidOperationException("Jump host user cannot be empty.");
            }

            if (profile.AuthMode == JumpHostAuthMode.Password)
            {
                return TestPasswordProfile(profile);
            }

            var localPort = FindAvailableLocalPort();
            var remoteProbePort = Math.Max(1, profile.Port);
            var tempKeyFilePath = string.Empty;
            var sshProcess = null as Process;

            try
            {
                SettingsStorage.EnsureApplicationDataDirectory();
                Directory.CreateDirectory(KnownHostsDirectory);
                EnsureKnownHostsFileExists();

                if (profile.AuthMode == JumpHostAuthMode.EmbeddedPrivateKey)
                {
                    var privateKeyContent = SecretVault.LoadSecret(profile.SecretRefId);
                    if (string.IsNullOrWhiteSpace(privateKeyContent))
                    {
                        throw new InvalidOperationException("No SSH private key is stored for the selected jump host profile.");
                    }

                    tempKeyFilePath = TempKeyMaterializer.MaterializePrivateKey(privateKeyContent);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = ResolveSshPath(),
                    Arguments = BuildArguments(profile, "127.0.0.1", remoteProbePort, localPort, tempKeyFilePath),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                sshProcess = Process.Start(startInfo);
                if (sshProcess == null)
                {
                    throw new InvalidOperationException("Failed to start ssh.exe.");
                }

                WaitForTunnelReady(sshProcess, localPort, profile.ConnectTimeoutSeconds);
                return string.Format("SSH test succeeded for {0}@{1}:{2}.", profile.User, profile.Host, profile.Port);
            }
            finally
            {
                CleanupProcess(sshProcess);
                TempKeyMaterializer.Delete(tempKeyFilePath);
            }
        }

        public static void ShutdownActiveSessions()
        {
            ActiveTunnelSession[] sessions;
            lock (SyncRoot)
            {
                sessions = ActiveSessions.ToArray();
                ActiveSessions.Clear();
            }

            foreach (var session in sessions)
            {
                CleanupSession(session);
            }
        }

        public static void CleanupTemporaryFiles()
        {
            TempKeyMaterializer.CleanupTemporaryFiles();
        }

        private static void RegisterSession(ActiveTunnelSession session)
        {
            lock (SyncRoot)
            {
                ActiveSessions.Add(session);
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                var activeSession = state as ActiveTunnelSession;
                if (activeSession == null)
                {
                    return;
                }

                try
                {
                    if (activeSession.MstscProcess != null)
                    {
                        activeSession.MstscProcess.WaitForExit();
                    }
                }
                catch
                {
                }
                finally
                {
                    RemoveSession(activeSession);
                    CleanupSession(activeSession);
                }
            }, session);
        }

        private static void RemoveSession(ActiveTunnelSession session)
        {
            lock (SyncRoot)
            {
                ActiveSessions.Remove(session);
            }
        }

        private static void CleanupSession(ActiveTunnelSession session)
        {
            if (session == null)
            {
                return;
            }

            CleanupProcess(session.SshProcess);
            CleanupManagedTunnel(session.ManagedPort, session.ManagedClient);

            if (session.MstscProcess != null)
            {
                try
                {
                    session.MstscProcess.Dispose();
                }
                catch
                {
                }
            }

            TempKeyMaterializer.Delete(session.TempKeyFilePath);
        }

        private static void LaunchWithPasswordTunnel(RdpEntry entry, JumpHostProfile profile)
        {
            var targetHost = string.IsNullOrWhiteSpace(entry.TunnelTargetHostOverride)
                ? (entry.Host ?? string.Empty).Trim()
                : entry.TunnelTargetHostOverride.Trim();
            if (string.IsNullOrWhiteSpace(targetHost))
            {
                throw new InvalidOperationException("Tunnel target host cannot be empty.");
            }

            var targetPort = ParsePort(string.IsNullOrWhiteSpace(entry.TunnelTargetPortOverride) ? entry.Port : entry.TunnelTargetPortOverride);
            var localPort = FindAvailableLocalPort();
            SshClient client = null;
            ForwardedPortLocal forwardedPort = null;

            try
            {
                client = CreatePasswordClient(profile);
                client.Connect();

                forwardedPort = new ForwardedPortLocal("127.0.0.1", (uint)localPort, targetHost, (uint)targetPort);
                client.AddForwardedPort(forwardedPort);
                forwardedPort.Start();
                WaitForManagedTunnelReady(client, forwardedPort, localPort, profile.ConnectTimeoutSeconds);

                var mstscProcess = RdpLauncher.LaunchToEndpoint("127.0.0.1", localPort, entry.User, entry.Password, true);
                RegisterSession(new ActiveTunnelSession
                {
                    ManagedClient = client,
                    ManagedPort = forwardedPort,
                    MstscProcess = mstscProcess
                });
            }
            catch
            {
                CleanupManagedTunnel(forwardedPort, client);
                throw;
            }
        }

        private static string TestPasswordProfile(JumpHostProfile profile)
        {
            SshClient client = null;
            try
            {
                client = CreatePasswordClient(profile);
                client.Connect();
                return string.Format("SSH test succeeded for {0}@{1}:{2}.", profile.User, profile.Host, profile.Port);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("SSH password authentication failed. " + ex.Message, ex);
            }
            finally
            {
                CleanupManagedTunnel(null, client);
            }
        }

        private static SshClient CreatePasswordClient(JumpHostProfile profile)
        {
            var password = LoadPassword(profile);
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("No SSH password is stored for the selected jump host profile.");
            }

            var client = new SshClient(profile.Host, Math.Max(1, profile.Port), profile.User, password);
            client.KeepAliveInterval = TimeSpan.FromSeconds(Math.Max(15, profile.KeepAliveSeconds));
            return client;
        }

        private static void WaitForManagedTunnelReady(SshClient client, ForwardedPortLocal forwardedPort, int localPort, int timeoutSeconds)
        {
            var timeoutMilliseconds = Math.Max(10, timeoutSeconds) * 1000;
            var startedAt = Environment.TickCount;

            while (Environment.TickCount - startedAt < timeoutMilliseconds)
            {
                if (client == null || !client.IsConnected)
                {
                    throw new InvalidOperationException("SSH tunnel disconnected before it became ready.");
                }

                if (forwardedPort != null && forwardedPort.IsStarted &&
                    (IsLocalPortListening(localPort) || CanConnectLocalPort(localPort)))
                {
                    return;
                }

                Thread.Sleep(200);
            }

            throw new InvalidOperationException("Timed out while waiting for the SSH tunnel to become ready.");
        }

        private static void CleanupManagedTunnel(ForwardedPortLocal forwardedPort, SshClient client)
        {
            if (forwardedPort != null)
            {
                try
                {
                    if (forwardedPort.IsStarted)
                    {
                        forwardedPort.Stop();
                    }
                }
                catch
                {
                }
                finally
                {
                    try
                    {
                        forwardedPort.Dispose();
                    }
                    catch
                    {
                    }
                }
            }

            if (client != null)
            {
                try
                {
                    if (client.IsConnected)
                    {
                        client.Disconnect();
                    }
                }
                catch
                {
                }
                finally
                {
                    try
                    {
                        client.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void CleanupProcess(Process process)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(2000);
                }
            }
            catch
            {
            }
            finally
            {
                try
                {
                    process.Dispose();
                }
                catch
                {
                }
            }
        }

        private static void WaitForTunnelReady(Process sshProcess, int localPort, int timeoutSeconds)
        {
            var timeoutMilliseconds = Math.Max(15, timeoutSeconds) * 1000;
            var startedAt = Environment.TickCount;

            while (Environment.TickCount - startedAt < timeoutMilliseconds)
            {
                if (sshProcess.HasExited)
                {
                    var error = ReadProcessOutput(sshProcess);
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(error)
                            ? "ssh.exe exited before the tunnel became ready."
                            : "ssh.exe failed. " + error);
                }

                // The SSH client can take several seconds before it starts accepting
                // loopback connections. Treat a bound listener as ready even if our
                // immediate TCP probe races the final forward activation.
                if (IsLocalPortListening(localPort) || CanConnectLocalPort(localPort))
                {
                    return;
                }

                Thread.Sleep(200);
            }

            throw new InvalidOperationException("Timed out while waiting for the SSH tunnel to become ready.");
        }

        private static bool IsLocalPortListening(int port)
        {
            try
            {
                return IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners()
                    .Any(endpoint => endpoint != null &&
                        endpoint.Port == port &&
                        (IPAddress.Any.Equals(endpoint.Address) ||
                         IPAddress.IPv6Any.Equals(endpoint.Address) ||
                         IPAddress.Loopback.Equals(endpoint.Address) ||
                         IPAddress.IPv6Loopback.Equals(endpoint.Address)));
            }
            catch
            {
                return false;
            }
        }

        private static bool CanConnectLocalPort(int port)
        {
            TcpClient client = null;
            try
            {
                client = new TcpClient();
                var result = client.BeginConnect("127.0.0.1", port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(250);
                if (!success)
                {
                    return false;
                }

                client.EndConnect(result);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (client != null)
                {
                    client.Close();
                }
            }
        }

        private static string BuildArguments(JumpHostProfile profile, string targetHost, int targetPort, int localPort, string tempKeyFilePath)
        {
            var parts = new List<string>
            {
                "-N",
                "-o", "ExitOnForwardFailure=yes",
                "-o", "StrictHostKeyChecking=accept-new",
                "-o", "UserKnownHostsFile=" + QuoteArgument(KnownHostsFilePath),
                "-o", "LogLevel=ERROR",
                "-o", "ConnectTimeout=" + Math.Max(3, profile.ConnectTimeoutSeconds),
                "-o", "ServerAliveInterval=" + Math.Max(15, profile.KeepAliveSeconds),
                "-p", Math.Max(1, profile.Port).ToString()
            };

            if (profile.AuthMode == JumpHostAuthMode.Password)
            {
                parts.Add("-o");
                parts.Add("BatchMode=no");
                parts.Add("-o");
                parts.Add("PreferredAuthentications=password");
                parts.Add("-o");
                parts.Add("PubkeyAuthentication=no");
                parts.Add("-o");
                parts.Add("PasswordAuthentication=yes");
                parts.Add("-o");
                parts.Add("KbdInteractiveAuthentication=no");
                parts.Add("-o");
                parts.Add("NumberOfPasswordPrompts=1");
            }
            else
            {
                parts.Add("-o");
                parts.Add("BatchMode=yes");
                parts.Add("-o");
                parts.Add("PreferredAuthentications=publickey");
                parts.Add("-o");
                parts.Add("PasswordAuthentication=no");
                parts.Add("-o");
                parts.Add("KbdInteractiveAuthentication=no");
            }

            if (profile.AuthMode == JumpHostAuthMode.EmbeddedPrivateKey)
            {
                parts.Add("-o");
                parts.Add("IdentitiesOnly=yes");
                parts.Add("-i");
                parts.Add(QuoteArgument(tempKeyFilePath));
            }

            parts.Add("-L");
            parts.Add(string.Format("127.0.0.1:{0}:{1}:{2}", localPort, targetHost, targetPort));
            parts.Add(string.Format("{0}@{1}", profile.User, profile.Host));

            return string.Join(" ", parts.ToArray());
        }

        private static string LoadPassword(JumpHostProfile profile)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(profile.RuntimePassword))
            {
                return profile.RuntimePassword;
            }

            return SecretVault.LoadSecret(profile.SecretRefId);
        }

        private static string ResolveSshPath()
        {
            var windowsSsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "OpenSSH", "ssh.exe");
            if (File.Exists(windowsSsh))
            {
                return windowsSsh;
            }

            return "ssh.exe";
        }

        private static void EnsureKnownHostsFileExists()
        {
            if (!File.Exists(KnownHostsFilePath))
            {
                File.WriteAllText(KnownHostsFilePath, string.Empty, new UTF8Encoding(false));
            }
        }

        private static int FindAvailableLocalPort()
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static int ParsePort(string rawPort)
        {
            int port;
            return int.TryParse((rawPort ?? string.Empty).Trim(), out port) && port > 0 && port <= 65535
                ? port
                : 3389;
        }

        private static string ReadProcessOutput(Process process)
        {
            try
            {
                var error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(error))
                {
                    return error.Trim();
                }

                var output = process.StandardOutput.ReadToEnd();
                return output.Trim();
            }
            catch
            {
                return string.Empty;
            }
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

        private sealed class ActiveTunnelSession
        {
            public Process SshProcess { get; set; }

            public Process MstscProcess { get; set; }

            public SshClient ManagedClient { get; set; }

            public ForwardedPortLocal ManagedPort { get; set; }

            public string TempKeyFilePath { get; set; }
        }
    }
}
