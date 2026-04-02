# Sitemap

## Navigation sitemap

```mermaid
flowchart TD
    A["RDP Manager"] --> B["All connections"]
    A --> C["Favorites"]
    A --> D["Recent"]
    A --> E["Cloudmini Sync"]
    A --> F["Settings"]

    B --> B1["Entry editor"]
    B --> B2["Row actions"]
    B --> B3["Connect flow"]

    C --> C1["Filtered list"]
    C --> C2["Entry editor"]

    D --> D1["Recent-only list"]
    D --> D2["Sort by LastConnectedUtc"]

    E --> E1["Token setup"]
    E --> E2["Connection test"]
    E --> E3["Fetch VPS"]
    E --> E4["Preview merge"]
    E --> E5["Apply sync"]

    F --> F1["CSV path"]
    F --> F2["Token storage"]
    F --> F3["Sync defaults"]
    F --> F4["About and diagnostics"]
```

## Navigation inventory

| Key | Type | Phase | Purpose |
| --- | --- | --- | --- |
| all-connections | List view | 1 | Hien toan bo entry |
| favorites | List view | 1 | Hien entry favorite |
| recent | List view | 1 | Hien entry moi connect |
| cloudmini-sync | Workflow view | 2 | Dong bo VPS tu Cloudmini |
| settings | Config view | 2 | Cau hinh app va token |

## Add/remove tab process

1. Them tab vao bang inventory.
2. Them 1 file spec moi trong `docs/ui`.
3. Cap nhat flowchart.
4. Them acceptance criteria cho tab.
5. Sau do moi them code UI.

## Sidebar order rule

Thu tu sidebar khuyen nghi:

1. All connections
2. Favorites
3. Recent
4. Cloudmini Sync
5. Settings

Ly do:

- 3 tab dau la daily use
- Cloudmini Sync la workflow theo dot
- Settings la utility
