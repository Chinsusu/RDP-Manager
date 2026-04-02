# Changelog

Tat ca thay doi dang ke cua du an se duoc ghi tai day.

## [Unreleased]

### Changed

- Card `Saved connections` va `Entry editor` duoc giu trong cung viewport o do phan giai hien tai, khong con phu thuoc vao scroll toan trang.
- Them phan trang cho danh sach local, mac dinh `10 items/page`.
- Cot `Host`, `User`, `Password` trong local list co the click de copy vao clipboard.
- Bo hanh vi double-click mo RDP; tu nay chi nut `Connect` moi duoc phep launch session.
- Tinh chinh do rong cot `Action` va nut trong bang de khong bi cat o man hinh hien tai.

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
