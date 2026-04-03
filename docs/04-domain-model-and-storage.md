# Domain Model And Storage

## Domain entities

## RdpEntry

Core fields:

- HostName
- Host
- Port
- User
- Password
- TransportMode
- JumpHostProfileId
- TunnelTargetHostOverride
- TunnelTargetPortOverride
- GroupName
- Tags
- Notes
- IsFavorite
- LastConnectedUtc
- HealthStatus
- LastHealthCheckedUtc

Phase 2 proposed fields:

- SourceProvider
- SourceId
- SourceStatus
- SourceLocation
- SourceCreatedAt
- SourceExpiredAt
- LastSyncedUtc
- IsProviderManaged

## CloudminiVps

Raw provider DTO:

- Pk
- Ip
- User
- Password
- Port
- CreatedAt
- ExpiredAt
- Cpu
- Ram
- Disk
- Price
- Location
- Status

## SyncResult

- NewCount
- UpdatedCount
- SkippedCount
- ConflictCount
- ErrorCount
- Entries

## SyncDecision

- CreateNew
- UpdateExisting
- Skip
- Conflict

## Storage strategy

## Current runtime storage

- `%AppData%\\RdpManager\\rdp-manager.db`
- `%AppData%\\RdpManager\\settings.user.xml`
- `%AppData%\\RdpManager\\secrets.user.xml`

## Compatibility files

- `clients.csv` de import/export
- `clients.meta.xml` de import metadata legacy
- `%AppData%\\RdpManager\\jump-hosts.user.xml` de migrate legacy proxy profiles mot lan

## Storage ownership

- `SQLite` la runtime source of truth cho connection data, local metadata, provider metadata, va proxy profiles
- CSV chi con vai tro import/export va backup
- user settings chua token, sync preference, UI preference
- SSH auth material phai nam trong protected store / user settings layer, khong duoc nam trong CSV

## SSH secret storage design

- `rdp-manager.db` chua metadata profile cua `Proxy server`
- `secrets.user.xml` chua blob da duoc ma hoa bang user-scoped protected storage
- app hien tai dung `SSH password auth` tren GUI; password khong duoc luu plaintext trong DB

## Why not put everything into CSV

- CSV de import/export va debug tay
- Provider metadata va token la du lieu co cau truc hon
- Token khong nen song chung voi credential CSV
- Khi co `Cloudmini sync`, `Notes`, `Group`, `Health`, va `Proxy server`, du lieu da vuot qua nguong hop voi CSV lam storage chinh

## SQLite schema overview

## connections

- `entry_key`
- `host_name`
- `host`
- `port`
- `user_name`
- `password`
- `transport_mode`
- `jump_host_profile_id`
- `tunnel_target_host_override`
- `tunnel_target_port_override`
- `group_name`
- `tags`
- `notes`
- `is_favorite`
- `last_connected_utc`
- `health_status`
- `last_health_checked_utc`
- `source_provider`
- `source_id`
- `source_status`
- `source_location`
- `source_created_at_utc`
- `source_expired_at_utc`
- `last_synced_utc`
- `is_provider_managed`

## proxy_profiles

- `id`
- `name`
- `host`
- `port`
- `user_name`
- `auth_mode`
- `secret_ref_id`
- `passphrase_secret_ref_id`
- `imported_key_label`
- `use_agent`
- `strict_host_key_checking_mode`
- `host_key_fingerprint`
- `connect_timeout_seconds`
- `keep_alive_seconds`

## Merge key design

Primary key for synced provider items:

- `SourceProvider + SourceId`

Fallback match key:

- `Host + Port + User`

## Mapping rules from CloudminiVps to RdpEntry

| Cloudmini field | Local field | Rule |
| --- | --- | --- |
| `pk` | `SourceId` | string or int stored as string |
| `ip` | `Host` | direct map |
| `port` | `Port` | direct map, default 3389 if missing |
| `user` | `User` | direct map |
| `password` | `Password` | overwrite only if sync option cho phep |
| `location` | `SourceLocation` | metadata only |
| `status` | `SourceStatus` | metadata only |

## HostName generation rule

Neu local entry moi duoc tao tu Cloudmini:

- Default HostName format: `<pk> - <ip>`

Neu user da sua HostName local:

- Khong overwrite khi sync, tru khi user chon cho phep

## Compatibility note

- CSV 4 cot cu van phai doc duoc
- CSV 5 cot moi la format chuan hien tai
- Metadata migration phai backward-compatible
- App se import `meta.xml` vao `SQLite` khi migrate
- `Notes`, `Group`, `Tags`, va `Health` khong lam thay doi format CSV; khi export, chung se di cung file `meta.xml`
- `JumpHostProfileId` va tunnel settings khong nam trong CSV

## Migration rule

- Neu `rdp-manager.db` chua co data va `clients.csv` ton tai, app se import CSV + metadata vao DB luc startup
- Neu `proxy_profiles` table rong va `jump-hosts.user.xml` ton tai, app se import proxy profiles legacy vao DB luc startup
- Sau migration, file cu khong bi xoa; chung chi tro thanh fallback/import source
