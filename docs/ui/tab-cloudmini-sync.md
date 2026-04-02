# Tab Design: Cloudmini Sync

## Purpose

Tab nay dung de ket noi Cloudmini API, tai danh sach VPS, preview thay doi, va sync vao local RDP list.

## Phase

Phase 2

## User stories

- Toi muon nhap API token va test ket noi.
- Toi muon xem danh sach VPS dang co tren Cloudmini.
- Toi muon sync ve local list ma khong lam mat custom HostName local.
- Toi muon biet item nao la moi, item nao la update, item nao bi bo qua.

## Layout proposal

## Zone A: Connection setup

- Provider badge: `Cloudmini`
- Token input
- `Test connection`
- `Save token`
- `Fetch VPS`

## Zone B: Account summary

- Account balance
- Credit
- Last sync time
- Last sync result

## Zone C: Remote VPS table

Columns:

- Select
- VPS ID
- IP
- Port
- User
- Status
- Location
- Expired at
- Sync action/result

## Zone D: Merge preview

- New entries count
- Updated entries count
- Skipped entries count
- Conflict entries count
- Option:
  - Keep local HostName
  - Overwrite password from provider
  - Import only online VPS

## Zone E: Footer actions

- `Sync selected`
- `Sync all`
- `Refresh remote list`
- `Cancel`

## States

## Empty state

- Chua co token
- Chua fetch VPS

## Loading state

- Dang test token
- Dang fetch VPS
- Dang apply sync

## Error state

- Invalid token
- Network timeout
- API response malformed
- Duplicate merge key conflict

## Interaction flow

1. User nhap token.
2. User bam `Test connection`.
3. App goi `GET /account`.
4. Neu hop le, cho phep `Fetch VPS`.
5. App goi `GET /vps`.
6. App map response thanh preview rows.
7. User chon mode sync.
8. App merge vao local store.
9. App hien summary ket qua.

## Data rules

- Cloudmini item phai duoc danh dau source = `cloudmini`.
- Merge key uu tien:
  - `SourceProvider + SourceId`
  - fallback `Host + Port + User`
- `HostName` local khong bi overwrite mac dinh.
- `Password` tu provider co the overwrite neu user bat option.

## Out of scope for Phase 2

- Order VPS
- Order proxy
- Full action execution
- Auto sync scheduler

## Acceptance criteria

- Token co the duoc validate.
- Remote VPS list duoc tai va hien thi day du.
- Sync khong tao duplicate neu VPS da ton tai.
- User nhin thay ro ket qua sync.
