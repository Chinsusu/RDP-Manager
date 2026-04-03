using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class SqliteStorage
    {
        public static string GetDatabasePath()
        {
            return Path.Combine(SettingsStorage.GetApplicationDataDirectory(), "rdp-manager.db");
        }

        public static void EnsureInitialized(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new InvalidOperationException("Database path cannot be empty.");
            }

            SettingsStorage.EnsureApplicationDataDirectory();
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(databasePath))
            {
                SQLiteConnection.CreateFile(databasePath);
            }

            using (var connection = OpenConnection(databasePath))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS app_meta (
    key TEXT PRIMARY KEY,
    value TEXT
);

CREATE TABLE IF NOT EXISTS connections (
    entry_key TEXT PRIMARY KEY,
    host_name TEXT,
    host TEXT NOT NULL,
    port TEXT NOT NULL,
    user_name TEXT,
    password TEXT,
    transport_mode TEXT,
    jump_host_profile_id TEXT,
    tunnel_target_host_override TEXT,
    tunnel_target_port_override TEXT,
    group_name TEXT,
    tags TEXT,
    notes TEXT,
    is_favorite INTEGER NOT NULL DEFAULT 0,
    last_connected_utc TEXT,
    health_status TEXT,
    last_health_checked_utc TEXT,
    source_provider TEXT,
    source_id TEXT,
    source_status TEXT,
    source_location TEXT,
    source_created_at_utc TEXT,
    source_expired_at_utc TEXT,
    last_synced_utc TEXT,
    is_provider_managed INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS proxy_profiles (
    id TEXT PRIMARY KEY,
    name TEXT,
    host TEXT,
    port INTEGER NOT NULL,
    user_name TEXT,
    auth_mode INTEGER NOT NULL,
    secret_ref_id TEXT,
    passphrase_secret_ref_id TEXT,
    imported_key_label TEXT,
    use_agent INTEGER NOT NULL DEFAULT 0,
    strict_host_key_checking_mode TEXT,
    host_key_fingerprint TEXT,
    connect_timeout_seconds INTEGER NOT NULL,
    keep_alive_seconds INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_connections_source ON connections(source_provider, source_id);
CREATE INDEX IF NOT EXISTS idx_connections_group_name ON connections(group_name);
";
                command.ExecuteNonQuery();
            }
        }

        public static void MigrateLegacyDataIfNeeded(string databasePath, string legacyCsvPath, string legacyProxyProfilesPath)
        {
            EnsureInitialized(databasePath);

            if (!HasConnections(databasePath) && !string.IsNullOrWhiteSpace(legacyCsvPath) && File.Exists(legacyCsvPath))
            {
                var importedEntries = CsvStorage.Load(legacyCsvPath);
                MetadataStorage.Apply(legacyCsvPath, importedEntries);
                SaveConnections(databasePath, importedEntries);
            }

            if (!HasProxyProfiles(databasePath) &&
                !string.IsNullOrWhiteSpace(legacyProxyProfilesPath) &&
                File.Exists(legacyProxyProfilesPath))
            {
                var profiles = JumpHostProfileStorage.LoadLegacyXml(legacyProxyProfilesPath);
                SaveProxyProfiles(databasePath, profiles);
            }
        }

        public static ObservableCollection<RdpEntry> LoadConnections(string databasePath)
        {
            EnsureInitialized(databasePath);

            var result = new ObservableCollection<RdpEntry>();
            using (var connection = OpenConnection(databasePath))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    host_name,
    host,
    port,
    user_name,
    password,
    transport_mode,
    jump_host_profile_id,
    tunnel_target_host_override,
    tunnel_target_port_override,
    group_name,
    tags,
    notes,
    is_favorite,
    last_connected_utc,
    health_status,
    last_health_checked_utc,
    source_provider,
    source_id,
    source_status,
    source_location,
    source_created_at_utc,
    source_expired_at_utc,
    last_synced_utc,
    is_provider_managed
FROM connections
ORDER BY
    CASE WHEN host_name IS NULL OR host_name = '' THEN host ELSE host_name END COLLATE NOCASE,
    host COLLATE NOCASE,
    user_name COLLATE NOCASE;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var entry = new RdpEntry
                        {
                            HostName = ReadString(reader, 0),
                            Host = ReadString(reader, 1),
                            Port = string.IsNullOrWhiteSpace(ReadString(reader, 2)) ? "3389" : ReadString(reader, 2),
                            User = ReadString(reader, 3),
                            Password = ReadString(reader, 4),
                            TransportMode = ParseTransportMode(ReadString(reader, 5)),
                            JumpHostProfileId = ReadString(reader, 6),
                            TunnelTargetHostOverride = ReadString(reader, 7),
                            TunnelTargetPortOverride = ReadString(reader, 8),
                            GroupName = ReadString(reader, 9),
                            Tags = ReadString(reader, 10),
                            Notes = ReadString(reader, 11),
                            IsFavorite = ReadBoolean(reader, 12),
                            LastConnectedUtc = ParseDate(ReadString(reader, 13)),
                            HealthStatus = ReadString(reader, 14),
                            LastHealthCheckedUtc = ParseDate(ReadString(reader, 15)),
                            SourceProvider = ReadString(reader, 16),
                            SourceId = ReadString(reader, 17),
                            SourceStatus = ReadString(reader, 18),
                            SourceLocation = ReadString(reader, 19),
                            SourceCreatedAtUtc = ParseDate(ReadString(reader, 20)),
                            SourceExpiredAtUtc = ParseDate(ReadString(reader, 21)),
                            LastSyncedUtc = ParseDate(ReadString(reader, 22)),
                            IsProviderManaged = ReadBoolean(reader, 23)
                        };

                        result.Add(entry);
                    }
                }
            }

            return result;
        }

        public static void SaveConnections(string databasePath, IEnumerable<RdpEntry> entries)
        {
            EnsureInitialized(databasePath);

            using (var connection = OpenConnection(databasePath))
            using (var transaction = connection.BeginTransaction())
            {
                using (var deleteCommand = connection.CreateCommand())
                {
                    deleteCommand.Transaction = transaction;
                    deleteCommand.CommandText = "DELETE FROM connections;";
                    deleteCommand.ExecuteNonQuery();
                }

                foreach (var entry in entries ?? Enumerable.Empty<RdpEntry>())
                {
                    using (var insertCommand = connection.CreateCommand())
                    {
                        insertCommand.Transaction = transaction;
                        insertCommand.CommandText = @"
INSERT INTO connections (
    entry_key,
    host_name,
    host,
    port,
    user_name,
    password,
    transport_mode,
    jump_host_profile_id,
    tunnel_target_host_override,
    tunnel_target_port_override,
    group_name,
    tags,
    notes,
    is_favorite,
    last_connected_utc,
    health_status,
    last_health_checked_utc,
    source_provider,
    source_id,
    source_status,
    source_location,
    source_created_at_utc,
    source_expired_at_utc,
    last_synced_utc,
    is_provider_managed
) VALUES (
    @entry_key,
    @host_name,
    @host,
    @port,
    @user_name,
    @password,
    @transport_mode,
    @jump_host_profile_id,
    @tunnel_target_host_override,
    @tunnel_target_port_override,
    @group_name,
    @tags,
    @notes,
    @is_favorite,
    @last_connected_utc,
    @health_status,
    @last_health_checked_utc,
    @source_provider,
    @source_id,
    @source_status,
    @source_location,
    @source_created_at_utc,
    @source_expired_at_utc,
    @last_synced_utc,
    @is_provider_managed
);";

                        AddParameter(insertCommand, "@entry_key", BuildEntryKey(entry));
                        AddParameter(insertCommand, "@host_name", entry.HostName);
                        AddParameter(insertCommand, "@host", (entry.Host ?? string.Empty).Trim());
                        AddParameter(insertCommand, "@port", string.IsNullOrWhiteSpace(entry.Port) ? "3389" : entry.Port.Trim());
                        AddParameter(insertCommand, "@user_name", entry.User);
                        AddParameter(insertCommand, "@password", entry.Password);
                        AddParameter(insertCommand, "@transport_mode", entry.TransportMode.ToString());
                        AddParameter(insertCommand, "@jump_host_profile_id", entry.JumpHostProfileId);
                        AddParameter(insertCommand, "@tunnel_target_host_override", entry.TunnelTargetHostOverride);
                        AddParameter(insertCommand, "@tunnel_target_port_override", entry.TunnelTargetPortOverride);
                        AddParameter(insertCommand, "@group_name", entry.GroupName);
                        AddParameter(insertCommand, "@tags", entry.Tags);
                        AddParameter(insertCommand, "@notes", entry.Notes);
                        AddParameter(insertCommand, "@is_favorite", entry.IsFavorite ? 1 : 0);
                        AddParameter(insertCommand, "@last_connected_utc", ToRoundTripString(entry.LastConnectedUtc));
                        AddParameter(insertCommand, "@health_status", entry.HealthStatus);
                        AddParameter(insertCommand, "@last_health_checked_utc", ToRoundTripString(entry.LastHealthCheckedUtc));
                        AddParameter(insertCommand, "@source_provider", entry.SourceProvider);
                        AddParameter(insertCommand, "@source_id", entry.SourceId);
                        AddParameter(insertCommand, "@source_status", entry.SourceStatus);
                        AddParameter(insertCommand, "@source_location", entry.SourceLocation);
                        AddParameter(insertCommand, "@source_created_at_utc", ToRoundTripString(entry.SourceCreatedAtUtc));
                        AddParameter(insertCommand, "@source_expired_at_utc", ToRoundTripString(entry.SourceExpiredAtUtc));
                        AddParameter(insertCommand, "@last_synced_utc", ToRoundTripString(entry.LastSyncedUtc));
                        AddParameter(insertCommand, "@is_provider_managed", entry.IsProviderManaged ? 1 : 0);
                        insertCommand.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
        }

        public static List<JumpHostProfile> LoadProxyProfiles(string databasePath)
        {
            EnsureInitialized(databasePath);

            var result = new List<JumpHostProfile>();
            using (var connection = OpenConnection(databasePath))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    id,
    name,
    host,
    port,
    user_name,
    auth_mode,
    secret_ref_id,
    passphrase_secret_ref_id,
    imported_key_label,
    use_agent,
    strict_host_key_checking_mode,
    host_key_fingerprint,
    connect_timeout_seconds,
    keep_alive_seconds
FROM proxy_profiles
ORDER BY CASE WHEN name IS NULL OR name = '' THEN host ELSE name END COLLATE NOCASE;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new JumpHostProfile
                        {
                            Id = ReadString(reader, 0),
                            Name = ReadString(reader, 1),
                            Host = ReadString(reader, 2),
                            Port = ReadInt(reader, 3, 22),
                            User = ReadString(reader, 4),
                            AuthMode = ParseJumpHostAuthMode(ReadInt(reader, 5, (int)JumpHostAuthMode.Password)),
                            SecretRefId = ReadString(reader, 6),
                            PassphraseSecretRefId = ReadString(reader, 7),
                            ImportedKeyLabel = ReadString(reader, 8),
                            UseAgent = ReadBoolean(reader, 9),
                            StrictHostKeyCheckingMode = string.IsNullOrWhiteSpace(ReadString(reader, 10)) ? "Ask" : ReadString(reader, 10),
                            HostKeyFingerprint = ReadString(reader, 11),
                            ConnectTimeoutSeconds = ReadInt(reader, 12, 10),
                            KeepAliveSeconds = ReadInt(reader, 13, 30)
                        });
                    }
                }
            }

            return result
                .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static void SaveProxyProfiles(string databasePath, IEnumerable<JumpHostProfile> profiles)
        {
            EnsureInitialized(databasePath);

            var normalized = JumpHostProfileStorage.NormalizeProfiles(profiles);
            using (var connection = OpenConnection(databasePath))
            using (var transaction = connection.BeginTransaction())
            {
                using (var deleteCommand = connection.CreateCommand())
                {
                    deleteCommand.Transaction = transaction;
                    deleteCommand.CommandText = "DELETE FROM proxy_profiles;";
                    deleteCommand.ExecuteNonQuery();
                }

                foreach (var profile in normalized)
                {
                    using (var insertCommand = connection.CreateCommand())
                    {
                        insertCommand.Transaction = transaction;
                        insertCommand.CommandText = @"
INSERT INTO proxy_profiles (
    id,
    name,
    host,
    port,
    user_name,
    auth_mode,
    secret_ref_id,
    passphrase_secret_ref_id,
    imported_key_label,
    use_agent,
    strict_host_key_checking_mode,
    host_key_fingerprint,
    connect_timeout_seconds,
    keep_alive_seconds
) VALUES (
    @id,
    @name,
    @host,
    @port,
    @user_name,
    @auth_mode,
    @secret_ref_id,
    @passphrase_secret_ref_id,
    @imported_key_label,
    @use_agent,
    @strict_host_key_checking_mode,
    @host_key_fingerprint,
    @connect_timeout_seconds,
    @keep_alive_seconds
);";

                        AddParameter(insertCommand, "@id", profile.Id);
                        AddParameter(insertCommand, "@name", profile.Name);
                        AddParameter(insertCommand, "@host", profile.Host);
                        AddParameter(insertCommand, "@port", profile.Port > 0 ? profile.Port : 22);
                        AddParameter(insertCommand, "@user_name", profile.User);
                        AddParameter(insertCommand, "@auth_mode", (int)profile.AuthMode);
                        AddParameter(insertCommand, "@secret_ref_id", profile.SecretRefId);
                        AddParameter(insertCommand, "@passphrase_secret_ref_id", profile.PassphraseSecretRefId);
                        AddParameter(insertCommand, "@imported_key_label", profile.ImportedKeyLabel);
                        AddParameter(insertCommand, "@use_agent", profile.UseAgent ? 1 : 0);
                        AddParameter(insertCommand, "@strict_host_key_checking_mode", profile.StrictHostKeyCheckingMode);
                        AddParameter(insertCommand, "@host_key_fingerprint", profile.HostKeyFingerprint);
                        AddParameter(insertCommand, "@connect_timeout_seconds", profile.ConnectTimeoutSeconds > 0 ? profile.ConnectTimeoutSeconds : 10);
                        AddParameter(insertCommand, "@keep_alive_seconds", profile.KeepAliveSeconds > 0 ? profile.KeepAliveSeconds : 30);
                        insertCommand.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
        }

        private static bool HasConnections(string databasePath)
        {
            return GetCount(databasePath, "connections") > 0;
        }

        private static bool HasProxyProfiles(string databasePath)
        {
            return GetCount(databasePath, "proxy_profiles") > 0;
        }

        private static int GetCount(string databasePath, string tableName)
        {
            using (var connection = OpenConnection(databasePath))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM " + tableName + ";";
                var scalar = command.ExecuteScalar();
                return scalar == null || scalar == DBNull.Value ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
            }
        }

        private static SQLiteConnection OpenConnection(string databasePath)
        {
            var connection = new SQLiteConnection("Data Source=" + databasePath + ";Version=3;");
            connection.Open();
            return connection;
        }

        private static void AddParameter(SQLiteCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static string BuildEntryKey(RdpEntry entry)
        {
            var host = (entry == null ? string.Empty : entry.Host ?? string.Empty).Trim().ToLowerInvariant();
            var port = (entry == null ? string.Empty : entry.Port ?? string.Empty).Trim().ToLowerInvariant();
            var user = (entry == null ? string.Empty : entry.User ?? string.Empty).Trim().ToLowerInvariant();
            return string.Format("{0}|{1}|{2}", host, port, user);
        }

        private static string ReadString(IDataRecord record, int ordinal)
        {
            return record.IsDBNull(ordinal) ? string.Empty : Convert.ToString(record.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static bool ReadBoolean(IDataRecord record, int ordinal)
        {
            if (record.IsDBNull(ordinal))
            {
                return false;
            }

            return Convert.ToInt32(record.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
        }

        private static int ReadInt(IDataRecord record, int ordinal, int defaultValue)
        {
            if (record.IsDBNull(ordinal))
            {
                return defaultValue;
            }

            int parsed;
            return int.TryParse(Convert.ToString(record.GetValue(ordinal), CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : defaultValue;
        }

        private static DateTime? ParseDate(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string ToRoundTripString(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("o", CultureInfo.InvariantCulture) : null;
        }

        private static TransportMode ParseTransportMode(string value)
        {
            TransportMode parsed;
            if (Enum.TryParse(value, true, out parsed))
            {
                return parsed;
            }

            return TransportMode.Direct;
        }

        private static JumpHostAuthMode ParseJumpHostAuthMode(int value)
        {
            return Enum.IsDefined(typeof(JumpHostAuthMode), value)
                ? (JumpHostAuthMode)value
                : JumpHostAuthMode.Password;
        }
    }
}
