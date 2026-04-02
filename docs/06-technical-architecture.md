# Technical Architecture

## Current state

Project hien tai la WPF app nho, logic phan lon nam trong `MainWindow.xaml.cs`, service layer gom:

- `CsvStorage`
- `MetadataStorage`
- `RdpLauncher`

Cach nay du cho Phase 1, nhung se kho scale khi them integration.

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

## RDP services

- `IRdpLauncher`

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

## Logging architecture

Recommended:

- file log simple text or rolling log
- level: Info, Warning, Error
- log operations:
  - app start
  - open/save csv
  - connect launch
  - cloudmini auth test
  - fetch vps
  - sync result

## Failure isolation

- Neu Cloudmini API loi, local RDP workflow van phai dung duoc
- Neu metadata file hong, CSV van phai load duoc
- Neu sync preview loi, khong duoc ghi de local storage
