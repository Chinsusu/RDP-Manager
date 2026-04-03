using System;
using System.IO;
using System.Text;

namespace RdpManager.Services
{
    public static class TempKeyMaterializer
    {
        private static readonly string TempDirectory = Path.Combine(SettingsStorage.GetApplicationDataDirectory(), "ssh-temp");

        public static string MaterializePrivateKey(string privateKeyContent)
        {
            if (string.IsNullOrWhiteSpace(privateKeyContent))
            {
                throw new InvalidOperationException("SSH private key content is empty.");
            }

            SettingsStorage.EnsureApplicationDataDirectory();
            Directory.CreateDirectory(TempDirectory);

            var filePath = Path.Combine(TempDirectory, "jump-key-" + Guid.NewGuid().ToString("N") + ".key");
            File.WriteAllText(filePath, privateKeyContent, new UTF8Encoding(false));

            try
            {
                File.SetAttributes(filePath, FileAttributes.Hidden | FileAttributes.Temporary);
            }
            catch
            {
            }

            return filePath;
        }

        public static void Delete(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                File.Delete(filePath);
            }
            catch
            {
            }
        }

        public static void CleanupTemporaryFiles()
        {
            if (!Directory.Exists(TempDirectory))
            {
                return;
            }

            var cutoff = DateTime.Now.AddDays(-1);
            foreach (var file in Directory.GetFiles(TempDirectory, "*.key"))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
