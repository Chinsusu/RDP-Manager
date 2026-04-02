# Tab Design: Recent

## Purpose

Tab nay hien thi nhung host vua duoc connect gan day.

## User stories

- Toi muon quay lai host vua mo nhanh.
- Toi muon nhin recent list ma khong can search.

## Data source

- Cung nguon `RdpEntry`
- Filter: `LastConnectedUtc != null`
- Sort: `LastConnectedUtc` descending

## Layout

- Giu cung layout voi `All connections`
- Co the hien them cot `Last connected` trong phase sau neu can

## Behavior rules

- Moi lan connect thanh cong, entry phai duoc update `LastConnectedUtc`.
- Tab Recent phai phan anh ngay sau connect.

## Empty state

- `No recent connections yet. Launch an RDP session once and it will appear here.`

## Acceptance criteria

- Thu tu recent dung theo lan connect gan nhat.
- Khong phai tao storage rieng cho recent.
- Khong duoc anh huong toi local CSV schema chinh.
