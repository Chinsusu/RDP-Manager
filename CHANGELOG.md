# Changelog

Tat ca thay doi dang ke cua du an se duoc ghi tai day.

## [Unreleased]

### Added

- Them bo docs thiet ke `SSH Tunnel Transport`, bao gom transport mode, jump host profile, security model, va checklist test.
- Them code-ready spec cho `Jump Host Profile`, `SecretVault`, `TempKeyMaterializer`, va connect flow 1-click qua SSH tunnel.
- Them backend `SSH Tunnel` milestone 2: app mo `ssh.exe` an, doi local forward san sang, roi launch `mstsc` qua `127.0.0.1:<localPort>`.
- Them protected `SecretVault`, `TempKeyMaterializer`, `JumpHostProfileStorage`, va `SshTunnelManager`.
- Them `Test SSH` cho `Jump Host Profile`, chay bang cung auth material va forward backend nhu flow tunnel that.
- Them cot `Proxy` trong `Saved connections`, kem kha nang doi `Direct` hoac `Proxy server` ngay tren tung row.

### Changed

- Local filter toolbar doi tu chip sang dropdown compact de tiet kiem ngang va de doc hon khi co them `Group`.
- Sidebar duoc chia lai thanh cac section `Search`, `Browse`, `Actions`, `Settings` de nhin gon hon.
- App gio dung custom title bar lien voi UI thay cho native title bar, kem nut `min / max / close` lon hon theo huong Win 11.
- Bang `Saved connections` khong con hien cot `HostName`; thu tu cot duoc doi de `Password` dung ngay sau `User`.
- `Connect` cho entry `SSH Tunnel` gio chay flow tunnel that thay vi chi hien placeholder message.
- UI doi ngon ngu tu `Jump Host` sang `Proxy server`; `Entry editor` dung 1 dropdown `Proxy` duy nhat thay cho `Transport + Jump host`.
- Proxy server editor duoc rut gon va mac dinh theo huong `SSH password auth`; cac lua chon key/agent khong con hien tren GUI.
- Entry editor va phan trang/footer duoc canh lai de khong bi day xuong khoi viewport o do phan giai hien tai.
- SSH tunnel khi mo `mstsc` vao `127.0.0.1:<localPort>` se tu dat `authentication level:i:0` de bo qua canh bao cert mismatch chi trong tunnel mode; `Direct` van giu muc canh bao mac dinh.

## [1.2.0-phase2-local-ops] - 2026-04-02

### Added

- Them `Notes`, `Group`, va `Tags` local cho moi `RdpEntry`, duoc luu trong `meta.xml`.
- Them `Group` filter tren local list.
- Them `Health` state cho local connections, kem `Check selected` va `Check page`.
- Them lookup health metadata: `HealthStatus`, `LastHealthCheckedUtc`.

### Changed

- Card `Saved connections` va `Entry editor` duoc giu trong cung viewport o do phan giai hien tai, khong con phu thuoc vao scroll toan trang.
- Them phan trang cho danh sach local, mac dinh `10 items/page`.
- Cot `Host`, `User`, `Password` trong local list co the click de copy vao clipboard.
- Search local list gio match ca `Group`, `Tags`, va `Notes`.
- Bo hanh vi double-click mo RDP; tu nay chi nut `Connect` moi duoc phep launch session.
- Tinh chinh do rong cot `Action` va nut trong bang de khong bi cat o man hinh hien tai.
- Heuristic phan loai `Linux` duoc doi thanh: `user = root`, neu khong thi moi fallback `port = 22`.

## [1.1.0-phase2-mvp] - 2026-04-02

### Added

- Them version tag hien thi trong GUI va diagnostics.
- Them tab `Cloudmini Sync`.
- Them tab `Settings`.
- Them Cloudmini API client cho `GET /account` va `GET /vps`.
- Them preview grid va merge workflow de sync VPS vao local RDP list.
- Them settings storage va token storage theo user machine.
- Them metadata provider cho `RdpEntry`: `SourceProvider`, `SourceId`, `SourceStatus`, `SourceLocation`, `LastSyncedUtc`.

### Changed

- Chuan hoa assembly version len `1.1.0.0`.
- UI gio co phan biet version/build de tranh nham binary cu.
- Cloudmini Sync dung button filter `All / Windows / Linux` thay vi dropdown.
- Trang local list co them platform filter `All / Windows / Linux`.
- Them nut `Back to connections` trong Settings de quay lai luong chinh ro rang hon.

### Notes

- Phase 2 hien dang o muc MVP.
- Build environment hien tai van dang compile tren `.NET Framework 4.0` voi warning `MSB3644`.
- Docs da chot muc tieu Phase 2 la retarget len `.NET Framework 4.8.1`, nhung may build hien tai chua co developer pack phu hop de retarget that su.

## [1.0.0] - 2026-04-01

### Added

- WPF GUI local-first de quan ly danh sach RDP.
- CRUD entry bang CSV.
- Search, Favorites, Recent.
- Launch `mstsc` voi credential luu trong CSV.
- Metadata file rieng cho favorite/recent.
