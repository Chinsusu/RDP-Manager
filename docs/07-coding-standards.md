# Coding Standards

## General principles

- Code de doc truoc, toi uu sau.
- UI logic khong chen vao storage logic.
- Moi integration ben ngoai phai di qua service/interface rieng.
- Khong update local storage trong luc chua co ket qua merge ro rang.

## Language and naming

- Class, method, property dung English.
- UI label co the la English de dong bo app hien tai.
- Variable name mo ta ro nghia, khong viet tat toi.

## File organization

- Moi class public mot file.
- View, ViewModel, Service tach folder rieng.
- DTO cua provider nam trong `Integrations/<Provider>/`.

## XAML rules

- Resource chung nam trong `App.xaml` hoac `Themes/`.
- Style khong duplicate neu co the dung chung.
- Control name dat theo vai tro:
  - `SearchTextBox`
  - `EntriesGrid`
  - `FavoriteButton`
- Event handler chi giu o muc glue code; business logic dua vao service or view-model.

## Code-behind rule

- Phase 1 co the chap nhan code-behind.
- Phase 2 tro di:
  - khong them merge logic vao code-behind
  - khong them HTTP call vao code-behind
  - khong parse JSON trong code-behind

## Storage rules

- CSV parser phai backward-compatible
- Settings va token khong luu chung trong CSV
- Moi file write phai atomic neu co the

## Error handling

- Khong swallow exception neu can thong tin cho user
- Neu catch de fallback, phai co log
- Message user phai ngan, ro, co action tiep theo

## Security rules

- Token khong log plain text
- Password Cloudmini khong log plain text
- Credential RDP dang la plain text do business requirement, nhung docs phai note ro risk
- SSH private key, certificate, passphrase khong duoc log plain text
- SSH secret khong duoc luu trong CSV hoac hardcode trong XAML / code-behind
- Secret storage phai di qua abstraction (`ISecretVault`), khong goi truc tiep protected API tung noi
- Temp key file phai duoc cleanup trong `finally`
- Runtime connect flow cho `SSH Tunnel` khong duoc phu thuoc vao password prompt cho user cuoi

## API integration rules

- Map response vao DTO truoc
- Validate payload truoc khi merge
- Khong bind directly JSON dynamic vao UI

## Pull request checklist

- Co cap nhat docs neu doi scope
- Co check backward compatibility cua CSV
- Co test local create/edit/connect
- Co test invalid token / valid token / empty result

## Documentation rules

- Feature moi phai co:
  - phase
  - acceptance criteria
  - UI impact
  - storage impact
  - rollback impact
