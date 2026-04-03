# SSH Tunnel Transport

## Purpose

Tai lieu nay mo ta transport mode `SSH Tunnel` de ep RDP di qua jump host ma van giu app local-first.

## Implementation status

Trang thai hien tai:

- milestone 1 da implemented: `TransportMode`, `JumpHostProfile`, `SecretVault`, Settings UI
- milestone 2 da implemented: tunnel backend, password-based auth flow, connect flow, cleanup, `Test SSH`

Chua implemented:

- shared SSH master
- target override UI
- host key fingerprint enforcement

## Scope

Phase dau chi ho tro:

- `Direct`
- `SSH Tunnel`

Khong lam trong scope nay:

- RD Gateway
- SOCKS/HTTP proxy tong quat
- Shared SSH master bat buoc cho moi moi truong

## Product rule

- Nhieu `RdpEntry` co the dung chung 1 `Proxy server` profile.
- User cuoi chi can bam `Connect`.
- SSH config khong duoc luu trong CSV.
- V1 uu tien non-interactive auth de user khong bi hoi password SSH khi connect.
- Voi tunnel mode, app duoc phep bo qua canh bao cert mismatch cua RDP vi ket noi loopback `127.0.0.1` khong khop CN/SAN cua may dich.

## Core decision

V1 se dung:

- 1 `ssh.exe` tunnel process cho moi phien RDP
- 1 local port rieng cho moi session
- cleanup tunnel khi `mstsc` thoat

V2 moi xem xet:

- shared SSH master
- dynamic forward add/remove tren 1 master process

## Why V1 first

- Don gian hon tren Windows
- De debug va cleanup
- It phu thuoc vao multiplexing behavior cua OpenSSH client tren may cuoi
- Khong block feature business chinh

## UX model

## Entry editor

Trang thai GUI hien tai:

- 1 field `Proxy`
- `Direct`
- danh sach `Proxy server` da luu

Rule:

- Neu `Proxy = Direct` thi entry di thang nhu RDP thong thuong
- Neu `Proxy = <profile>` thi app map thanh `SSH Tunnel` + `JumpHostProfileId`
- User khong can biet backend dang mo tunnel hay local port nao

## Settings

Them section `Proxy servers`:

- list profile
- add profile
- edit profile
- delete profile
- test SSH
- luu password an toan

`Test SSH` hien tai duoc implement bang ket noi SSH that den proxy server, xac thuc bang password da luu trong protected storage, sau do tra ket qua pass/fail ro rang tren GUI.

## Authentication strategy

## Current V1 GUI model

- `Password` la auth mode duy nhat hien tren GUI
- password SSH duoc luu trong `SecretVault`
- user cuoi khong can nhap lai password khi connect
- `Test SSH` va `Connect` dung chung secret da luu

## Backend implementation

- `Password` auth hien tai chay in-process bang `SSH.NET`
- app tao local forward `127.0.0.1:<localPort> -> targetHost:targetPort`
- sau khi local forward san sang, app mo `mstsc` vao `127.0.0.1:<localPort>`
- khi dong `mstsc`, app dong forwarded port va dispose `SshClient`

## Hidden / future modes

- `EmbeddedPrivateKey`
- `Agent`

Nhung cac mode nay hien khong duoc mo tren GUI chinh.

## Future model

- `EmbeddedCertificate` hoac short-lived SSH certificate
- app lay credential tam thoi tu broker noi bo

## Runtime flow

1. User bam `Connect`.
2. App resolve `TransportMode`.
3. Neu `Direct`, flow giong hien tai.
4. Neu `SSH Tunnel`:
   - load `Proxy server` profile
   - resolve auth material tu `SecretVault`
   - chon local port trong khoang an toan
   - mo tunnel qua `SSH.NET`
   - wait den khi local port open hoac timeout
   - goi `cmdkey` cho `TERMSRV/127.0.0.1`
   - launch `mstsc /v:127.0.0.1:<local>` voi `authentication level:i:0`
   - theo doi `mstsc`
   - cleanup tunnel khi `mstsc` dong
   - dispose session

## Implemented test flow

1. User bam `Test SSH`.
2. App build profile tu editor hien tai.
3. App tai password tu `SecretVault`.
4. App mo ket noi SSH that den proxy server.
5. Neu auth thanh cong, test pass.
6. App dong ket noi test ngay sau khi xac nhan.

## Data model

## RdpEntry additions

- `TransportMode`
- `JumpHostProfileId`
- `TunnelTargetHostOverride`
- `TunnelTargetPortOverride`

## JumpHostProfile

- `Id`
- `Name`
- `Host`
- `Port`
- `User`
- `AuthMode`
- `SecretRefId`
- `StrictHostKeyCheckingMode`
- `HostKeyFingerprint`
- `ConnectTimeoutSeconds`
- `KeepAliveSeconds`

## TunnelSession

Runtime only:

- `EntryId`
- `ProfileId`
- `LocalPort`
- `ProxyServerProfileId`
- `MstscProcessId`
- `StartedUtc`
- `Status`

## SecretVault payload

- `SecretId`
- `Kind`
- `CipherText`
- `CreatedUtc`
- `UpdatedUtc`

Secret kinds:

- `SshPassword`

## Security model

## What is possible

Co the dat muc tieu:

- user cuoi khong thay password SSH
- user cuoi khong can tu nhap credential SSH
- app chi can 1 click de connect

## What is not possible

Neu client tu mo SSH tunnel truc tiep thi phai co mot cach xac thuc tren chinh may client. Nghia la:

- khong the hoan toan "khong co credential o client"
- chi co the khong cho user cuoi thay credential, hoac dung credential ngan han

## Recommended security levels

Muc toi thieu cho V1:

- luu SSH password bang user-scoped protected storage
- khong luu trong CSV
- khong hien lai raw secret tren UI sau khi save

Tot hon:

- pin `HostKeyFingerprint`
- khoa jump host account bang `AllowTcpForwarding local` va `PermitOpen`

Tot nhat ve van hanh:

- dung short-lived SSH certificate hoac credential duoc cap tam thoi
- app lay credential ngan han tu broker noi bo
- client khong giu static SSH password lau dai

Neu muon thuc su khong de client giu bat ky SSH auth material nao thi can doi architecture:

- broker/gateway phia server
- hoac RD Gateway / jump service do server kiem soat

## Failure handling

- SSH auth fail: thong bao ro jump host auth fail
- Tunnel timeout: kill process, khong de process treo
- `mstsc` fail: cleanup tunnel ngay
- App restart: startup can cleanup best-effort cac session do app tao truoc do
- Managed tunnel cleanup fail: log warning va cleanup best-effort khi app start lan sau

## Code-ready service map

- `ITransportResolver`
- `IJumpHostProfileStorage`
- `ISecretVault`
- `ISshTunnelManager`
- `IPortAllocator`
- `ISessionWatcher`
- `IProcessTracker`

## Suggested folder layout

```text
Services/
  Transport/
    TransportMode.cs
    TransportResolver.cs
    DirectRdpLauncher.cs
    SshTunnelManager.cs
    PortAllocator.cs
    SessionWatcher.cs
  Security/
    SecretVault.cs
    ProtectedDataProtector.cs
  Storage/
    JumpHostProfileStorage.cs
Models/
  JumpHostProfile.cs
  TunnelSession.cs
  SecretRecord.cs
```

## Connect pseudocode

```text
Connect(entry):
  if entry.TransportMode == Direct:
      launch direct RDP
      return

  profile = JumpHostProfileStorage.Get(entry.JumpHostProfileId)
  secret = SecretVault.Get(profile.SecretRefId)
  localPort = PortAllocator.Reserve()
  tunnel = SshTunnelManager.OpenPasswordTunnel(profile, entry, localPort, secret)
  WaitForLocalPort(localPort, profile.ConnectTimeoutSeconds)
  CacheRdpCredential("127.0.0.1", localPort, entry.User, entry.Password)
  mstsc = LaunchMstsc(localPort, ignoreCertificateWarnings=true)
  SessionWatcher.Bind(mstsc, tunnel)
```

## UI state rules

- `Proxy = Direct`: entry di thang
- `Proxy = Proxy server`: entry di qua SSH tunnel
- Sau khi luu secret, UI hien `Stored securely` thay vi value that
- Sau khi `Test SSH`, UI hien status line thanh cong / that bai ngay trong card editor
- Row `Proxy` trong `Saved connections` co the doi nhanh `Direct` hay `Proxy server` bang dropdown inline

## Implementation milestones

1. Them model `TransportMode`, `JumpHostProfile`, `SecretVault`
2. Them Settings UI cho `Proxy servers`
3. Them protected storage cho SSH password
4. Them `SshTunnelManager`
5. Noi `Connect` flow voi cleanup
6. Manual QA tunnel lifecycle

## Acceptance criteria

- User co the tao `Proxy server` profile va test ket noi SSH
- 1 `Proxy server` profile co the duoc gan cho nhieu `RdpEntry`
- `Connect` qua `SSH Tunnel` khong lam vo flow `Direct`
- Tunnel phai duoc cleanup sau khi dong `mstsc`
- SSH secret khong nam trong CSV
- UI khong show raw secret sau khi da luu
- V1 connect 1-click khong duoc yeu cau user nhap lai SSH password
- Tunnel mode khong duoc bi chan boi popup cert mismatch cua `mstsc`
