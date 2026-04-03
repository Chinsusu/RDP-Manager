# Technical Architecture

## Current state

Project hien tai la WPF app nho, logic phan lon nam trong `MainWindow.xaml.cs`, service layer gom:

- `CsvStorage`
- `MetadataStorage`
- `RdpLauncher`
- `CloudminiClient`
- `CloudminiSyncService`
- `SettingsStorage`
- `JumpHostProfileStorage`
- `SshTunnelManager`
- `SecretVault`
- `TempKeyMaterializer`

Cach nay du cho Phase 1, nhung se kho scale khi them integration.

Trang thai thuc te hien tai:

- Phase 2 MVP da chay duoc trong codebase hien tai.
- UI da co nhieu state hon truoc: local filter, cloud filter, pagination, sync preview, token settings.
- `MainWindow.xaml.cs` hien dang ganh ca coordination logic UI va workflow logic, day la diem can tach tiep trong buoc hardening.
- `SSH Tunnel` milestone 2 da duoc noi vao connect flow, nhung van chua tach khoi code-behind theo huong MVVM-lite.

## Target architecture for Phase 2+

Khuyen nghi chuyen sang service-oriented MVVM-lite:

- `Views`
- `ViewModels`
- `Models`
- `Services`
- `Integrations`
- `Infrastructure`

## Suggested folder structure

```text
RdpManager/
  Views/
  ViewModels/
  Models/
  Services/
    Storage/
    Sync/
    Security/
  Integrations/
    Cloudmini/
      CloudminiClient.cs
      CloudminiDto.cs
      CloudminiMapper.cs
  Infrastructure/
    Logging/
    Configuration/
```

## Core service boundaries

## Storage services

- `ICsvStorage`
- `IMetadataStorage`
- `ISettingsStorage`
- `IJumpHostProfileStorage`
- `ISecretVault`

## RDP services

- `IRdpLauncher`
- `ITransportResolver`
- `ISshTunnelManager`
- `ISshCommandBuilder`
- `IPortAllocator`
- `ISessionWatcher`

## Integration services

- `ICloudminiClient`
- `ICloudminiSyncService`
- `IConnectionMergeService`

## View-model responsibilities

- `MainWindowViewModel`
- `ConnectionsViewModel`
- `EntryEditorViewModel`
- `CloudminiSyncViewModel`
- `SettingsViewModel`

## Why move away from code-behind

- De test logic merge ma khong can spin UI
- De them tab moi khong lam file `MainWindow.xaml.cs` phinh to
- De tach state cua local list va cloud sync

## Key architecture decisions

## Decision 1

List views dung chung 1 domain model `RdpEntry`, khac nhau o filter/sort.

## Decision 2

Cloudmini data khong bind truc tiep vao local list. Phai di qua merge service.

## Decision 3

Provider metadata luu trong metadata/settings layer, khong nhan het vao CSV.

## Decision 4

Phase 2 nen retarget len `.NET Framework 4.8.1`.

Reasons:

- TLS/HTTPS modern hon
- Tooling de dang hon
- HttpClient / security / packaging de bao tri hon
- Day la buoc nang cap it xao tron nhat cho project WPF `.NET Framework` hien tai
- Chua can ganh chi phi migrate sang `.NET 8.0` chi de phuc vu Cloudmini sync

## Decision 5

Transport `SSH Tunnel` duoc them theo kieu adapter, khong sua thang `RdpLauncher` thanh mot class om tat ca.

Architecture de xuat:

- `TransportResolver`
- `DirectRdpLauncher`
- `SshTunnelManager`
- `JumpHostProfileStorage`
- `SessionWatcher`

Trang thai implemented hien tai:

- `RdpLauncher` da ho tro launch vao endpoint tuy chon
- `SshTunnelManager` dang giu lifecycle tunnel + cleanup
- `TempKeyMaterializer` dang materialize private key tam thoi
- `SecretVault` dang luu auth material bang protected storage

## Decision 6

V1 cua `SSH Tunnel` se dung 1 `ssh.exe` process cho moi phien RDP. Shared SSH master chi la V2 optimization.

Reasons:

- Don gian de theo doi PID
- Cleanup ro rang hon
- It rui ro hon tren may Windows khong dong deu ve OpenSSH config

## Security architecture for SSH transport

- SSH secret khong duoc luu trong CSV
- Secret luu bang protected storage, user-scoped
- App UI khong duoc show lai raw secret sau khi da save
- Neu sau nay co broker cap short-lived credential, `SshTunnelManager` phai goi qua abstraction thay vi doc secret truc tiep tu UI
- V1 khong nen dua vao SSH password prompt trong runtime connect flow

## Logging architecture

Recommended:

- file log simple text or rolling log
- level: Info, Warning, Error
- log operations:
  - app start
  - open/save csv
  - connect launch
  - tunnel start
  - tunnel stop
  - tunnel timeout / auth fail
  - cloudmini auth test
  - fetch vps
  - sync result

## Failure isolation

- Neu Cloudmini API loi, local RDP workflow van phai dung duoc
- Neu metadata file hong, CSV van phai load duoc
- Neu sync preview loi, khong duoc ghi de local storage
