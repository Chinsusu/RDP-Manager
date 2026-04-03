# Roadmap And Phases

## Delivery strategy

Du an duoc phat trien theo tung phase nho, moi phase co acceptance criteria ro rang. Khong nhay thang vao full cloud management.

## Phase 1: Local RDP list

Status: done, can continue polish

Deliverables:

- Local CSV storage
- GUI quan ly entry
- Search
- Favorites
- Recent
- Connect through `mstsc`

Known tech debt:

- Logic dang nam nhieu o code-behind
- Chua co logging chuan
- Chua co test automation

## Phase 2: Cloudmini sync

Status: in progress, MVP da co

Target:

- Tich hop Cloudmini API vao GUI
- Sync VPS list tu Cloudmini ve local list
- Co merge strategy de tranh duplicate va tranh overwrite sai
- Co token validation va sync feedback

Recommended prerequisite:

- Retarget project len `.NET Framework 4.8.1`
- Tach service layer va view-model layer ro hon

Technical decision for this phase:

- Khong migrate sang `.NET 8.0` trong Phase 2
- Uu tien `.NET Framework 4.8.1` de giam rui ro migration va tap trung vao Cloudmini sync

Deliverables:

- Tab Cloudmini Sync
- Settings cho token
- Provider metadata cho `RdpEntry`
- Manual sync
- Sync result summary
- Platform filter `All / Windows / Linux`
- Local list pagination
- Explicit connect flow, khong auto launch bang double-click

Current implemented scope:

- Test token qua `GET /account`
- Fetch VPS qua `GET /vps`
- Preview sync va merge vao local RDP list
- Luu token theo user machine neu bat remember
- Filter VPS Linux/Windows theo heuristic `user = root`, fallback `port = 22`
- Filter local list Linux/Windows bang cung heuristic
- Copy nhanh `Host`, `User`, `Password` tu local list
- Pagination local list `10 items/page`
- Local metadata fields: `Notes`, `Group`, `Tags`
- Local health check cho selected entry va current page

Acceptance criteria:

- User nhap token va test duoc ket noi
- App load duoc `GET /vps`
- App import hoac update duoc danh sach local
- App hien ro item moi, item duoc update, item bo qua
- User co the dieu huong UI ma khong bi ket o Settings
- Local list khong tu mo RDP khi double-click row
- Local metadata khong can chen vao CSV van phai tim kiem va su dung duoc
- Health check khong duoc pha vo local CRUD flow

## Phase 3: Cloudmini actions

Status: optional backlog

Target:

- Trigger `reboot`
- Trigger `start`
- Trigger `stop`
- Trigger `reset_password`
- Hien ket qua action trong UI

Acceptance criteria:

- Action co confirm dialog
- Co loading state
- Co thong bao thanh cong / that bai

## Phase 4: SSH tunnel transport

Status: in progress, milestone 2 da implemented

Target:

- Them `Transport mode`: `Direct | SSH Tunnel`
- Cho phep nhieu connection dung chung `Jump Host Profile`
- Mo `mstsc` thong qua local forwarded port
- Luu SSH config ngoai CSV

Deliverables:

- `Jump Hosts` section trong Settings
- `Transport` field trong Entry editor
- Tunnel lifecycle manager
- Tunnel cleanup khi `mstsc` dong
- Secret storage cho SSH auth material
- `Test SSH` cho jump host profile

Current implemented scope:

- Them `Transport = Direct | SSH Tunnel` tren local entry
- Them `Jump Hosts` editor trong Settings
- Them `JumpHostProfileStorage`, `SecretVault`, `TempKeyMaterializer`
- Them `SshTunnelManager` mo `ssh.exe` an va cleanup khi `mstsc` dong
- Them `Test SSH` bang flow forward ngan de verify auth + forwarding

Current limitations:

- Chua co shared SSH master / multiplexing
- Chua expose target override UI cho tunnel target host/port
- Host key fingerprint moi duoc luu, chua duoc enforce trong command builder

Acceptance criteria:

- User co the connect 1 RDP entry qua jump host bang 1 click
- 1 profile co the dung lai cho nhieu entry
- `Direct` mode van hoat dong nhu cu
- SSH secret khong nam trong CSV
- Tunnel loi khong lam crash app

## Phase 5: Hardening

Status: backlog

Target:

- Logging
- Structured error messages
- Backup/restore
- Better release packaging
- Manual regression checklist

## Suggested milestone plan

1. Freeze current Phase 1 UI and behavior.
2. Retarget framework len `.NET Framework 4.8.1` va clean architecture boundary.
3. Implement Cloudmini client and DTO mapping.
4. Implement Cloudmini Sync tab UI.
5. Implement sync preview and merge.
6. Manual QA on real Cloudmini account.
7. Tach bot logic khoi `MainWindow.xaml.cs`, retarget len `.NET Framework 4.8.1`, roi release Phase 2.
8. Implement `SSH Tunnel` transport voi V1 architecture.
9. Hardening, logging, va packaging.

## Change control rule

- Moi feature moi phai map vao 1 phase.
- Feature nao chua co phase thi vao backlog, khong code chen ngang.
- Khi phase doi scope, cap nhat docs truoc roi moi code.
