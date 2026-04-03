# SQLite Storage Migration

## Purpose

Tai lieu nay chot kien truc storage moi cua app:

- `SQLite` la storage chinh
- `CSV` chi con dung de import/export
- `settings` va `secrets` van tach rieng

## Why migrate

Storage cu bi tach manh:

- `clients.csv`
- `clients.meta.xml`
- `jump-hosts.user.xml`
- `settings.user.xml`
- `secrets.user.xml`

Khi app co them:

- `Cloudmini sync`
- `Notes / Group / Tags`
- `Health`
- `Proxy server`
- `SSH Tunnel`

thi `CSV + XML` khong con la runtime model hop ly nua.

## Scope

`SQLite` se chua:

- `connections`
- `proxy_profiles`

Van de ngoai DB:

- `settings.user.xml`
- `secrets.user.xml`

Ly do:

- settings va secret da co flow on dinh
- secret dang duoc bao ve bang `DPAPI`
- khong nen doi qua nhieu truc cung luc

## Database path

- `%AppData%\\RdpManager\\rdp-manager.db`

## Source of truth

Sau migration:

- app runtime doc/ghi vao DB
- `Import CSV` se nap CSV vao DB
- `Save database` se persist DB
- `Export CSV` se xuat snapshot tu DB

## Schema

## connections

Primary key:

- `entry_key = lower(trim(host)) + '|' + lower(trim(port)) + '|' + lower(trim(user))`

Columns:

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

Primary key:

- `id`

Columns:

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

## Migration rules

## Connections migration

Dieu kien:

- `connections` table rong
- `clients.csv` ton tai

Flow:

1. Load `clients.csv`
2. Neu co `clients.meta.xml` thi apply metadata legacy
3. Save vao `connections`

## Proxy profiles migration

Dieu kien:

- `proxy_profiles` table rong
- `jump-hosts.user.xml` ton tai

Flow:

1. Load XML profile cu
2. Normalize profile fields
3. Save vao `proxy_profiles`

## Non-destructive migration

- File legacy khong bi xoa
- Migration chi chay khi DB table rong
- User van co the import lai CSV bang tay neu can

## UX changes

- Sidebar:
  - `Import CSV`
  - `Save database`
  - `Export CSV`

- Diagnostics:
  - hien `Database: <path>`

## Compatibility

- App van doc duoc CSV 4 cot cu
- App van export CSV 5 cot chuan hien tai
- Metadata export van di ra `.meta.xml` khi export/backup CSV

## Acceptance criteria

- App tao duoc `rdp-manager.db` neu chua co
- App migrate duoc du lieu cu len DB
- CRUD local list doc/ghi DB dung
- Proxy profiles doc/ghi DB dung
- Cloudmini sync khong bi vo sau khi chuyen storage
- Backup pre-sync van tao duoc file CSV restoreable
