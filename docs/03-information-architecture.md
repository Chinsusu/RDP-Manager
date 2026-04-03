# Information Architecture

## Navigation principle

Navigation ben trai la hub cho cac "view cua danh sach" va "module chuc nang". Cac tab duoc chia thanh 2 nhom:

- Local list views
- Integration / Settings views

## Primary navigation tree

- All connections
- Favorites
- Recent
- Cloudmini Sync
- Settings

## View taxonomy

## List views

- All connections
- Favorites
- Recent

Cac view nay cung xai mot nguon du lieu `RdpEntry`, chi khac filter va sort rule.

## Workflow views

- Cloudmini Sync

View nay khong phai chi la filter. No co workflow rieng: auth, fetch, preview, merge.

## Configuration views

- Settings

View nay quan ly preference, storage path, token, sync defaults.

## Screen composition rule

Tat ca tab nen dung layout 3 tang:

1. Header
2. Main content
3. Context actions

Neu la list tab, main content chia thanh:

- Left/center: list area
- Right: detail/editor area
- Card list va card editor phai cao bang nhau trong viewport chinh, khong dung scroll toan trang de lo noi dung chinh.

Neu la workflow tab, main content chia thanh:

- Config area
- Result/preview area
- Footer action area

## Extensibility rule

De de them bot sua tab:

- Moi tab co 1 doc rieng trong `docs/ui`
- Sitemap la nguon su that cho navigation
- Code sau nay nen mapping `TabKey -> ViewModel -> View`
- Tab moi phai tu tra loi 5 cau hoi:
  - Tab nay dung de lam gi
  - Data source la gi
  - Action chinh la gi
  - Empty state la gi
  - Acceptance criteria la gi

## State ownership

- Local connection state thuoc `RdpEntry`
- Filter state thuoc current tab/view-model
- Cloudmini auth state thuoc integration settings
- Sync preview state thuoc Cloudmini Sync module

## Layout consistency rules

- Sidebar chi chua navigation va quick actions
- List tab phai co search box
- List tab nen uu tien filter compact dang dropdown neu can tiet kiem ngang hoac co tu 3 filter tro len tren cung mot hang
- Neu can filter theo taxonomy rong hon, uu tien 1 filter inline nho gon (vi du `Group`) thay vi them mot pane rieng
- List tab co nhieu item phai uu tien pagination hon la keo dai card
- Button destructive phai co style do nhat quan
- Nguon du lieu external phai co nhan provider/source
- Local-only metadata (`Notes`, `Group`, `Tags`, `Health`) phai song ngoai CSV de khong lam vo import/export flow
- Title bar custom neu duoc dung phai van giu du resize, maximize, minimize, va drag window nhu native window
