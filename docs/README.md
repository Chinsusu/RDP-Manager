# RDP Manager Project Docs

Bo tai lieu nay mo ta du an theo cach lam viec thuc te, de co the tiep tuc phat trien tung phase ma khong bi vo thiet ke.

## Current baseline

- Phase 1 da co: WPF GUI quan ly danh sach RDP bang CSV.
- App hien tai da ho tro: CRUD local entries, Favorites, Recent, search, launch `mstsc`.
- Phase tiep theo duoc dinh huong trong docs: tich hop Cloudmini API de sync VPS vao danh sach.
- Technical target cho Phase 2: retarget len `.NET Framework 4.8.1`.

## Doc map

- [01-product-overview.md](./01-product-overview.md)
  Tong quan san pham, muc tieu, scope, persona, non-goals.
- [02-roadmap-and-phases.md](./02-roadmap-and-phases.md)
  Lo trinh phat trien theo phase, milestone, dependency, backlog.
- [03-information-architecture.md](./03-information-architecture.md)
  Kien truc thong tin, nguyen tac mo rong, module map.
- [ui/sitemap.md](./ui/sitemap.md)
  Sitemap, navigation map, quy tac them bot tab.
- [ui/tab-all-connections.md](./ui/tab-all-connections.md)
  Thiet ke chi tiet tab All connections.
- [ui/tab-favorites.md](./ui/tab-favorites.md)
  Thiet ke chi tiet tab Favorites.
- [ui/tab-recent.md](./ui/tab-recent.md)
  Thiet ke chi tiet tab Recent.
- [ui/tab-cloudmini-sync.md](./ui/tab-cloudmini-sync.md)
  Thiet ke chi tiet tab Cloudmini Sync.
- [ui/tab-settings.md](./ui/tab-settings.md)
  Thiet ke chi tiet tab Settings.
- [04-domain-model-and-storage.md](./04-domain-model-and-storage.md)
  Domain model, storage strategy, merge key, sync metadata.
- [05-cloudmini-api-integration.md](./05-cloudmini-api-integration.md)
  Thiet ke tich hop Cloudmini API.
- [06-technical-architecture.md](./06-technical-architecture.md)
  Kien truc code, folder structure, service boundary, migration note.
- [07-coding-standards.md](./07-coding-standards.md)
  Coding standard, XAML standard, naming, security rules.
- [08-testing-release-and-operations.md](./08-testing-release-and-operations.md)
  Test strategy, release checklist, logging, support, backup.

## How to use this docs set

- Khi them tab moi: cap nhat `ui/sitemap.md`, tao them `ui/tab-<name>.md`, neu co domain moi thi cap nhat `04-domain-model-and-storage.md`.
- Khi them integration moi: tao doc tuong tu `05-cloudmini-api-integration.md`.
- Khi doi architecture: cap nhat `06-technical-architecture.md` va `07-coding-standards.md` truoc khi code.
