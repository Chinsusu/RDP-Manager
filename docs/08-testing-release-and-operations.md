# Testing Release And Operations

## Test strategy

Dung 3 lop test:

- Unit-level logic review
- Manual integration testing
- Release smoke test

## Phase 1 manual checklist

- Tao entry moi
- Sua entry cu
- Xoa entry
- Search theo HostName
- Search theo Host
- Search theo Group / Tags / Notes
- Favorite / unfavorite
- Recent update sau connect
- Save database
- Import CSV khac
- Export CSV snapshot
- Health check selected
- Health check current page

## Phase 2 manual checklist

- Token invalid
- Token valid
- `GET /account` thanh cong
- `GET /vps` thanh cong
- Empty VPS list
- Cloudmini filter `All / Windows / Linux`
- Sync all
- Sync selected
- Merge update khong duplicate
- Keep local HostName
- Overwrite password option

## Regression checklist

- Cloudmini loi khong duoc lam hong local list
- DB rong + `clients.csv` ton tai thi app phai migrate len `SQLite` duoc
- Import CSV khong duoc lam hong DB neu file metadata legacy bi hong
- Jump host settings hong khong duoc chan flow `Direct`
- Favorites/Recent van dung sau sync
- Connect sau sync van mo duoc `mstsc`
- Local list phan trang dung `10 items/page`
- Click `Host`, `User`, `Password` copy dung gia tri
- Double-click row khong duoc auto connect
- Settings co the quay lai `All connections`
- `Group` filter local list phai hoat dong dung
- `Notes`, `Group`, `Tags`, `Health` van con sau khi dong/mo lai app
- Dropdown filter local list va Cloudmini Sync phai mo/chon duoc binh thuong
- Custom title bar phai drag, maximize, minimize, close dung nhu native window

## SSH tunnel manual checklist

- Tao proxy server profile moi
- Sua proxy server profile
- Xoa proxy server profile
- Test SSH thanh cong
- Test SSH that bai
- Gan 1 profile cho nhieu `RdpEntry`
- Connect 1 entry qua `SSH Tunnel`
- Dong `mstsc` va verify tunnel duoc cleanup
- `Direct` mode van connect dung sau khi co profile SSH
- SSH secret khong nam trong CSV
- App restart khong de tunnel zombie do app tao ra
- Tunnel mode khong hien popup cert mismatch cua `mstsc`

## Release checklist

1. Build Release thanh cong.
2. Chay smoke test mo app.
3. Verify `SQLite` DB duoc tao duoi `%AppData%\\RdpManager\\rdp-manager.db`.
4. Verify `clients.csv` backward-compatible.
5. Verify token storage khong nam trong CSV.
6. Verify log folder create duoc.
7. Cap nhat docs version neu doi scope lon.
8. Neu co `SSH Tunnel`, verify startup cleanup va tunnel cleanup khi dong `mstsc`.

## Support diagnostics

Nen co:

- app version
- current database path
- current export path neu co
- last sync time
- last error message

## Backup and restore

- Truoc sync bulk, co the tao backup `connections.<timestamp>.csv`
- Metadata export di cung backup CSV
- Restore flow phai don gian: import lai CSV cu vao DB

## Operations note

- App la desktop local-first tool
- Neu provider API outage, local operations van phai tiep tuc
- Sync duoc thiet ke la manual-first, khong auto scheduler trong phase 2
- Runtime data nam trong SQLite; export CSV la snapshot/interop, khong con la source of truth
