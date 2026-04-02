# Tab Design: All Connections

## Purpose

Day la tab trung tam cua app. Moi thao tac local phai co the bat dau tu day.

## User stories

- Toi muon xem toan bo danh sach host.
- Toi muon tim nhanh theo HostName, Host, User.
- Toi muon sua thong tin va connect ngay.
- Toi muon tao entry moi ma khong ghi de entry cu.

## Layout

- Header:
  - Title: `Saved connections`
  - Subtitle: huong dan copy va connect ro rang
- Main list area:
  - Search box o sidebar
  - Platform filter chips: `All`, `Windows`, `Linux`
  - Data grid / list grid
  - Pagination footer, mac dinh `10 items/page`
  - Row actions: Connect, Edit, Delete
- Right panel:
  - Entry editor
  - Favorite toggle
  - Apply entry
  - Clear form

## Main components

- Search input
- Platform filter chips
- Connections grid
- Pagination controls
- Entry editor
- Global quick actions

## Row columns

- HostName
- Host
- Port
- User
- Password (masked)
- Action

## Row actions

- Connect
- Edit
- Delete

## Direct row interactions

- Click `Host` -> copy host vao clipboard
- Click `User` -> copy user vao clipboard
- Click `Password` -> copy password thuc vao clipboard
- Double-click row khong duoc phep auto launch RDP
- Chi nut `Connect` o row hoac `Connect selected` moi duoc mo session

## Validation rules

- `Host` la required
- `Port` default la `3389`
- `HostName` la display label, co the de trong
- Password duoc luu plain text theo yeu cau business hien tai

## Editor behavior

- `Add new entry` dua form vao create mode.
- `Apply entry` o create mode phai tao row moi.
- `Apply entry` o edit mode phai update row dang edit.
- Sau khi create thanh cong, form co the reset ve create mode de nhap tiep.

## States

## Empty state

- `No connections found. Add a new entry or open another CSV file.`

## Loading state

- Khong can loading spinner cho local storage.

## Error state

- Validation message neu `Host` rong
- Message box neu launch `mstsc` that bai

## Acceptance criteria

- User co the tao, sua, xoa, connect tu mot tab duy nhat.
- Search phan hoi theo text nhap.
- Connect row khong phu thuoc vao editor state sai.
- Create mode va edit mode phai tach ro rang.
- Local list khong lam cao vo han theo so item; phai giu trong viewport bang pagination.
- `Host`, `User`, `Password` co the copy bang 1 click.
- Ket noi RDP chi xay ra khi user bam `Connect`.
