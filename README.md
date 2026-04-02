# RDP Manager

WPF app nho gon de quan ly danh sach RDP trong file CSV va mo `mstsc` khi click.

Trang thai hien tai:

- local list + CRUD + Favorites + Recent
- Cloudmini Sync MVP
- platform filter `Windows / Linux`
- pagination local list `10 items/page`
- click-to-copy cho `Host`, `User`, `Password`

## Project docs

Bo tai lieu thiet ke day du nam tai [docs/README.md](./docs/README.md).

## Changelog

Lich su thay doi nam tai [CHANGELOG.md](./CHANGELOG.md).

## CSV format

App doc file cu 4 cot va tu dong ghi format moi 5 cot:

```csv
HostName,Host,Port,User,Password
Client A,10.0.0.10,3389,administrator,secret123
DC-01,rdp.example.com,3390,corp\\john,P@ssw0rd
```

## Build

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe .\RdpManager.csproj /p:Configuration=Release
```

## Run

```powershell
.\bin\Release\RdpManager.exe
```

Neu `clients.csv` chua ton tai ben canh file `.exe`, app tu tao file moi voi header mac dinh.
