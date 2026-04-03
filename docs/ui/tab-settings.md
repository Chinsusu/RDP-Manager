# Tab Design: Settings

## Purpose

Tab nay dung de quan ly setting muc ung dung, khong chua daily workflow.

## Phase

Phase 2

## Sections

## General

- Default CSV path
- Open last used file on startup
- Confirm before delete

## Security

- Store Cloudmini token
- Option `Remember token on this machine`
- Button `Clear saved token`
- Quan ly `Proxy server` profile
- SSH secret phai dung protected storage

## Proxy Servers

- Proxy server profile list
- Add profile
- Edit profile
- Delete profile
- Test SSH
- Test SSH phai chay bang cung backend auth/tunnel config nhu flow connect that
- Khong hien raw secret sau khi save
- Hien trang thai `Stored securely` cho password da luu
- GUI hien tai mac dinh theo `SSH password auth`

## Sync defaults

- Keep local HostName by default
- Overwrite password by default
- Import only online VPS by default

## Diagnostics

- App version
- Current data file path
- Last sync timestamp
- Open log folder

## Storage rule

- Settings khong duoc luu trong CSV
- Token neu luu, phai dung user-scoped encrypted storage

## Acceptance criteria

- Settings tach khoi connection data
- Token khong duoc lo ra UI sau khi luu
- User co the reset settings ma khong mat CSV
- Proxy server profile co the dung lai cho nhieu connection
- SSH auth material khong duoc nam trong CSV
- `Test SSH` phai bao ket qua ro rang ma khong bat user xem log backend
