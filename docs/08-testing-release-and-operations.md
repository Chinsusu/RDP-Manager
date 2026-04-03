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
- Save CSV
- Open CSV khac
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
- Metadata file hong van phai load duoc CSV
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

- Tao jump host profile moi
- Sua jump host profile
- Xoa jump host profile
- Test SSH thanh cong
- Test SSH that bai
- Test SSH fail ro rang khi chua import key o `Embedded key`
- Test SSH pass o `Agent` neu may co `ssh-agent` va identity hop le
- Gan 1 profile cho nhieu `RdpEntry`
- Connect 1 entry qua `SSH Tunnel`
- Dong `mstsc` va verify tunnel duoc cleanup
- `Direct` mode van connect dung sau khi co profile SSH
- SSH secret khong nam trong CSV
- App restart khong de tunnel zombie do app tao ra

## Release checklist

1. Build Release thanh cong.
2. Chay smoke test mo app.
3. Verify `clients.csv` backward-compatible.
4. Verify token storage khong nam trong CSV.
5. Verify log folder create duoc.
6. Cap nhat docs version neu doi scope lon.
7. Neu co `SSH Tunnel`, verify startup cleanup va tunnel cleanup khi dong `mstsc`.

## Support diagnostics

Nen co:

- app version
- current csv path
- current metadata path
- last sync time
- last error message

## Backup and restore

- Truoc sync bulk, co the tao backup `clients.<timestamp>.csv`
- Metadata file cung duoc backup cung cap
- Restore flow phai don gian: chon lai CSV cu

## Operations note

- App la desktop local-first tool
- Neu provider API outage, local operations van phai tiep tuc
- Sync duoc thiet ke la manual-first, khong auto scheduler trong phase 2
