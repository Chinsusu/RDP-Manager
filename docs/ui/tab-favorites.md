# Tab Design: Favorites

## Purpose

Tab nay la mot filtered view cua `All connections`, danh cho host duoc su dung thuong xuyen.

## User stories

- Toi muon loc nhanh cac host quan trong.
- Toi muon danh dau bo favorite tu editor.
- Toi muon connect tu favorites ma khong can ve tab chinh.

## Data source

- Cung nguon `RdpEntry`
- Filter: `IsFavorite == true`

## Layout

- Giu cung layout voi `All connections`
- Khong tao editor rieng
- Khong co workflow rieng

## UX rules

- Neu entry dang favorite bi unstar, row bien mat khoi tab sau khi refresh.
- Favorite toggle phai de o editor header de de thay doi.

## Empty state

- `No favorite connections yet. Select an entry and click the star in Entry editor.`

## Acceptance criteria

- Filter favorite phai dung ngay sau khi toggle.
- Khong tao duplicate UI logic.
- Connect, Edit, Delete phai hoat dong nhu All connections.
