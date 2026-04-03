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

## Current files

- `clients.csv`
- `clients.meta.xml`

## Proposed additional files

- `%AppData%\\RdpManager\\settings.user.json`
- `%AppData%\\RdpManager\\logs\\`
- `%AppData%\\RdpManager\\jump-hosts.user.json`
- `%AppData%\\RdpManager\\secrets.user.json`

## Storage ownership

- CSV chi chua RDP credential data can ban
- `meta.xml` chua local metadata (`favorite`, `recent`, `group`, `tags`, `notes`, `health`) va provider metadata
- user settings chua token, sync preference, UI preference
- jump host profiles va SSH auth material phai nam trong protected store / user settings layer, khong duoc nam trong CSV

## SSH secret storage design

- `jump-hosts.user.json` chi chua metadata profile
- `secrets.user.json` chua blob da duoc ma hoa bang user-scoped protected storage
- app co the materialize private key ra temp file khi connect, nhung khong luu plaintext lau dai tren disk

## Why not put everything into CSV

- CSV de import/export va debug tay
- Provider metadata va token la du lieu co cau truc hon
- Token khong nen song chung voi credential CSV

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
- `Notes`, `Group`, `Tags`, va `Health` khong lam thay doi format CSV; chung chi song trong `meta.xml`
- `TransportMode` co the duoc ghi vao metadata neu can backward-compatible voi CSV hien tai
- `JumpHostProfileId` va tunnel settings phai song ngoai CSV de tranh lo SSH config
