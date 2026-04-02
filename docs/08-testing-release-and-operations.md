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
- Favorites/Recent van dung sau sync
- Connect sau sync van mo duoc `mstsc`
- Local list phan trang dung `10 items/page`
- Click `Host`, `User`, `Password` copy dung gia tri
- Double-click row khong duoc auto connect
- Settings co the quay lai `All connections`
- `Group` filter local list phai hoat dong dung
- `Notes`, `Group`, `Tags`, `Health` van con sau khi dong/mo lai app

## Release checklist

1. Build Release thanh cong.
2. Chay smoke test mo app.
3. Verify `clients.csv` backward-compatible.
4. Verify token storage khong nam trong CSV.
5. Verify log folder create duoc.
6. Cap nhat docs version neu doi scope lon.

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
