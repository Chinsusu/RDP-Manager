# Product Overview

## Product name

RDP Manager

## Problem statement

Nguoi dung can mot desktop app nho, don gian, de:

- luu danh sach thong tin dang nhap RDP
- mo nhanh `mstsc` chi bang 1 click
- quan ly nhieu VPS/client tren cung 1 giao dien
- trong phase tiep theo, co the dong bo VPS tu nha cung cap Cloudmini

## Product goals

- Giam thao tac mo `Remote Desktop Connection` thu cong.
- Chuyen thong tin ket noi thanh danh sach co the tim kiem, loc, sua.
- Ho tro dong bo VPS tu API de tranh nhap tay.
- Giu UI don gian, toc do mo nhanh, de mo rong.

## Non-goals

- Khong nhung session RDP vao trong app.
- Khong thay the `mstsc` bang 1 RDP engine rieng.
- Khong quan ly order thanh toan, billing, hay full lifecycle cloud trong phase 2.
- Khong quan ly proxy chung hay RD Gateway trong pham vi docs nay.

## Primary personas

## Persona A: Internal operator

- Quan ly 10-100 VPS Windows.
- Can login nhanh.
- Can search theo host, IP, username.
- Co nhu cau danh dau favorite va recent.

## Persona B: VPS reseller / support operator

- Mua VPS tu Cloudmini.
- Can lay thong tin VPS moi tu API va dua vao danh sach RDP.
- Can biet VPS nao la local entry, VPS nao la sync tu provider.

## Core use cases

1. Tao entry RDP thu cong.
2. Chinh sua credential.
3. Connect nhanh vao 1 host.
4. Loc theo favorite.
5. Loc theo recent.
6. Sync danh sach VPS tu Cloudmini.
7. Merge du lieu Cloudmini vao local list ma khong lam mat note va custom display name.

## Scope by phase

## Phase 1

- GUI local list RDP
- CSV storage
- Favorites / Recent
- Search
- Row actions: Connect, Edit, Delete

## Phase 2

- Cloudmini API authentication
- Load VPS list tu API
- Preview sync
- Import vao local list
- Merge/update by source metadata

## Phase 3

- Optional Cloudmini actions: reboot, start, stop, reset password
- Token health check
- Last sync history

## Success metrics

- Tao va connect mot entry local trong duoi 20 giay.
- Sync danh sach VPS Cloudmini trong duoi 5 giay voi account nho.
- Ty le duplicate sai khi sync < 1% sau khi ap dung merge key.
- Nguoi dung co the nhin ro entry nao la local, entry nao la Cloudmini.

## Constraints

- App hien tai dang la WPF tren `.NET Framework 4.0`.
- Build environment dang co warning `MSB3644`.
- De tich hop HTTPS API on dinh va maintainable, phase 2 nen retarget len `.NET Framework 4.8.1`.
- Phase 2 khong chon `.NET 8.0`; uu tien nang cap it rui ro nhat tu project WPF `.NET Framework` hien tai.
