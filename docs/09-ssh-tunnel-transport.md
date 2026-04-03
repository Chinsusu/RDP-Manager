# SSH Tunnel Transport

## Purpose

Tai lieu nay mo ta transport mode `SSH Tunnel` de ep RDP di qua jump host ma van giu app local-first.

## Implementation status

Trang thai hien tai:

- milestone 1 da implemented: `TransportMode`, `JumpHostProfile`, `SecretVault`, Settings UI
- milestone 2 da implemented: tunnel backend, temp key materialization, connect flow, cleanup, `Test SSH`

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

- Nhieu `RdpEntry` co the dung chung 1 `JumpHostProfile`.
- User cuoi chi can bam `Connect`.
- SSH config khong duoc luu trong CSV.
- V1 uu tien non-interactive auth de user khong bi hoi password SSH khi connect.

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

Them field:

- `Transport`: `Direct | SSH Tunnel`
- `Jump host profile`
- `Tunnel target host override` optional
- `Tunnel target port override` optional

Rule:

- Neu `Transport = Direct` thi an cac field tunnel
- Neu `Transport = SSH Tunnel` thi `Jump host profile` la required

## Settings

Them section `Jump Hosts`:

- list profile
- add profile
- edit profile
- delete profile
- test SSH
- import private key
- rotate secret

`Test SSH` hien tai duoc implement bang 1 local forward ngan toi `127.0.0.1:<sshPort>` tren jump host, de verify auth va forwarding permission ma khong mo RDP.

## Authentication strategy

## V1 supported auth modes

- `EmbeddedPrivateKey`
- `Agent`

## V1 not supported

- raw SSH password login cho 1-click tunnel

Ly do:

- `ssh.exe` khong phai lua chon tot cho flow password non-interactive
- user da chot yeu cau chi bam `Connect`, khong nhap lai credential
- key/cert de automation on dinh hon password prompt

## Preferred V1 model

- private key duoc import vao app
- key bytes duoc ma hoa bang user-scoped protected storage
- khi connect, app materialize ra temp key file co vong doi ngan
- `ssh.exe` duoc goi voi `-i <tempkey>`
- temp key file bi xoa ngay sau khi tunnel dung
- test SSH cung dung chung auth material va temp key strategy nay

## Future model

- `EmbeddedCertificate` hoac short-lived SSH certificate
- app lay credential tam thoi tu broker noi bo

## Runtime flow

1. User bam `Connect`.
2. App resolve `TransportMode`.
3. Neu `Direct`, flow giong hien tai.
4. Neu `SSH Tunnel`:
   - load `JumpHostProfile`
   - resolve auth material tu `SecretVault`
   - materialize temp key file neu auth mode la `EmbeddedPrivateKey`
   - chon local port trong khoang an toan
   - start `ssh.exe -N -L 127.0.0.1:<local>:<targetHost>:<targetPort> user@jumpHost`
   - wait den khi local port open hoac timeout
   - goi `cmdkey` cho `TERMSRV/127.0.0.1`
   - launch `mstsc /v:127.0.0.1:<local>`
   - theo doi `mstsc`
   - cleanup tunnel khi `mstsc` dong
   - xoa temp key file neu da tao

## Implemented test flow

1. User bam `Test SSH`.
2. App build profile tu editor hien tai.
3. Neu auth mode la `EmbeddedPrivateKey`, app lay key tu `SecretVault`.
4. App materialize temp key file neu can.
5. App mo `ssh.exe -N -L 127.0.0.1:<local>:127.0.0.1:<sshPort>`.
6. Neu local port listen thanh cong trong timeout, test pass.
7. App kill process test va xoa temp key file.

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
- `PassphraseSecretRefId`
- `ImportedKeyLabel`
- `UseAgent`
- `StrictHostKeyCheckingMode`
- `HostKeyFingerprint`
- `ConnectTimeoutSeconds`
- `KeepAliveSeconds`

## TunnelSession

Runtime only:

- `EntryId`
- `ProfileId`
- `LocalPort`
- `SshProcessId`
- `MstscProcessId`
- `TempKeyFilePath`
- `StartedUtc`
- `Status`

## SecretVault payload

- `SecretId`
- `Kind`
- `CipherText`
- `CreatedUtc`
- `UpdatedUtc`

Secret kinds:

- `SshPrivateKey`
- `SshPrivateKeyPassphrase`
- `SshCertificate`

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

- luu SSH secret bang user-scoped protected storage
- khong luu trong CSV
- khong hien lai raw secret tren UI sau khi save
- temp key file phai nam trong thu muc user-scoped, ACL han che, va xoa sau khi dung

Tot hon:

- dung private key duoc ma hoa
- uu tien `ssh-agent` neu moi truong co
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
- Temp key file cleanup fail: log warning va retry cleanup khi app start lan sau

## Code-ready service map

- `ITransportResolver`
- `IJumpHostProfileStorage`
- `ISecretVault`
- `ITempKeyMaterializer`
- `ISshTunnelManager`
- `ISshCommandBuilder`
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
    SshCommandBuilder.cs
    PortAllocator.cs
    SessionWatcher.cs
  Security/
    SecretVault.cs
    ProtectedDataProtector.cs
    TempKeyMaterializer.cs
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
  keyFile = TempKeyMaterializer.Materialize(secret)
  localPort = PortAllocator.Reserve()
  sshArgs = SshCommandBuilder.Build(profile, entry, localPort, keyFile)
  sshProcess = StartSsh(sshArgs)
  WaitForLocalPort(localPort, profile.ConnectTimeoutSeconds)
  CacheRdpCredential("127.0.0.1", localPort, entry.User, entry.Password)
  mstsc = LaunchMstsc(localPort)
  SessionWatcher.Bind(mstsc, sshProcess, keyFile)
```

## UI state rules

- `Transport = Direct`: an toan bo section jump host
- `Transport = SSH Tunnel`: hien profile selector va target override
- `AuthMode = EmbeddedPrivateKey`: hien `Import key`, `Replace key`, `Clear key`
- `AuthMode = Agent`: an phan import key, chi show note ve `ssh-agent`
- Sau khi luu secret, UI hien `Stored securely` thay vi value that
- Sau khi `Test SSH`, UI hien status line thanh cong / that bai ngay trong card editor

## Implementation milestones

1. Them model `TransportMode`, `JumpHostProfile`, `SecretVault`
2. Them Settings UI cho `Jump Hosts`
3. Them protected storage va temp key materializer
4. Them `SshTunnelManager`
5. Noi `Connect` flow voi cleanup
6. Manual QA tunnel lifecycle

## Acceptance criteria

- User co the tao `JumpHostProfile` va test ket noi SSH
- 1 `JumpHostProfile` co the duoc gan cho nhieu `RdpEntry`
- `Connect` qua `SSH Tunnel` khong lam vo flow `Direct`
- Tunnel phai duoc cleanup sau khi dong `mstsc`
- SSH secret khong nam trong CSV
- UI khong show raw secret sau khi da luu
- V1 connect 1-click khong duoc yeu cau user nhap lai SSH password
