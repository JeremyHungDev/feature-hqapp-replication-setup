# HqApi + BranchApi 設計文件

**日期：** 2026-04-15
**目標：** 將現有 lab 的單一 console app 拆成兩個獨立的 ASP.NET Core Web API，各自擁有並管理自己的 DB，並透過 PostgreSQL Logical Replication + NOTIFY 實現資料同步與即時通知。

---

## 情境回顧

```
HqApi (App1)                           BranchApi (App2)
├── POST /api/menu                      ├── GET /api/menu
├── PUT  /api/menu/{id}                 └── GET /api/notifications
└── GET  /api/menu                                ▲
          │                                       │ in-memory ConcurrentQueue
          ▼                              BackgroundService
        hq-db                             └── LISTEN 'menu_update'
          │                                           ▲
          │  Logical Replication                      │ pg_notify
          │  ├── 篩選 table（排除 employee_salary）   │
          │  └── 篩選 column（排除 menu.cost）        │
          └─────────────────────────────► branch-db (readonly)
                                           └── Trigger: trg_menu_notify
```

---

## 專案結構

```
lab-postgres-replication/
├── docker-compose.yml          ← 新增 hq-api、branch-api 兩個 service
├── HqApp/                      ← 保留供參考，不再執行
├── HqApi/                      ← 新增：HQ Web API
│   ├── HqApi.csproj
│   ├── Program.cs
│   ├── Dockerfile
│   ├── Controllers/
│   │   └── MenuController.cs   ← POST/PUT/GET /api/menu
│   ├── Data/
│   │   └── HqDbContext.cs      ← 複用現有（含 Menu、StorePolicy、EmployeeSalary）
│   ├── Models/
│   │   └── Menu.cs             ← 複用現有
│   └── Services/
│       └── ReplicationPublisherService.cs  ← 啟動時設定 Publication（冪等）
└── BranchApi/                  ← 新增：Branch Web API
    ├── BranchApi.csproj
    ├── Program.cs
    ├── Dockerfile
    ├── Controllers/
    │   ├── MenuController.cs         ← GET /api/menu
    │   └── NotificationsController.cs ← GET /api/notifications
    ├── Data/
    │   └── BranchDbContext.cs        ← 複用現有（含 BranchMenu、BranchStorePolicy）
    ├── Models/
    │   └── BranchMenu.cs             ← 複用現有
    └── Services/
        ├── ReplicationSubscriberService.cs  ← 啟動時設定 Subscription + Trigger + read-only（冪等）
        └── MenuNotificationListener.cs      ← BackgroundService，LISTEN 'menu_update'
```

---

## HqApi 設計

### 啟動流程

1. 等待 hq-db 就緒（retry loop）
2. `ReplicationPublisherService.SetupAsync()` 設定 Publication（冪等）
3. Web API 開始接受請求

### API Endpoints

| Method | Path | 說明 |
|--------|------|------|
| `GET` | `/api/menu` | 取得所有菜單（含 cost） |
| `POST` | `/api/menu` | 新增菜單項目 |
| `PUT` | `/api/menu/{id}` | 修改價格或上下架狀態 |

### ReplicationPublisherService

啟動時執行（冪等）：

```sql
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'pub_hq_to_branch') THEN
        CREATE PUBLICATION pub_hq_to_branch FOR TABLE
            menu (id, name, category, price, is_available, updated_at),
            store_policy;
    END IF;
END $$;
```

### Request / Response

**POST /api/menu**
```json
// Request
{ "name": "Oolong Tea", "category": "tea", "price": 110, "cost": 20 }

// Response 201
{ "id": 7, "name": "Oolong Tea", "category": "tea", "price": 110, "cost": 20, "isAvailable": true }
```

**PUT /api/menu/{id}**
```json
// Request（只需傳要修改的欄位）
{ "price": 130, "isAvailable": false }

// Response 200
{ "id": 1, "name": "Americano", "price": 130, "isAvailable": false, ... }
```

---

## BranchApi 設計

### 啟動流程

1. 等待 branch-db 就緒（retry loop）
2. `ReplicationSubscriberService.SetupAsync()` 設定 Subscription + Trigger + read-only（冪等）
3. `MenuNotificationListener`（BackgroundService）啟動，開始 LISTEN
4. Web API 開始接受請求

### API Endpoints

| Method | Path | 說明 |
|--------|------|------|
| `GET` | `/api/menu` | 取得 Branch 菜單（不含 cost） |
| `GET` | `/api/notifications` | 取得最近收到的 NOTIFY 訊息（最多 50 筆） |

### ReplicationSubscriberService

啟動時執行（冪等），依序：

1. 建立 Subscription（若不存在）：
```sql
-- 以 SELECT 檢查 pg_subscription，再 CREATE（不可包在 DO $$ 裡）
CREATE SUBSCRIPTION sub_branch_from_hq
    CONNECTION 'host=hq-db port=5432 dbname=coffee_shop user=hq_admin password=hq123'
    PUBLICATION pub_hq_to_branch;
```

2. 建立 NOTIFY Trigger（`CREATE OR REPLACE`，天然冪等）：
```sql
CREATE OR REPLACE FUNCTION notify_menu_change() ...
CREATE TRIGGER trg_menu_notify ...
ALTER TABLE menu ENABLE ALWAYS TRIGGER trg_menu_notify;
```

3. 設定 read-only（最後執行，避免影響上述 DDL）：
```sql
ALTER DATABASE coffee_shop SET default_transaction_read_only = on;
```

### MenuNotificationListener（BackgroundService）

```
啟動 → NpgsqlConnection.OpenAsync()
     → LISTEN menu_update
     → 迴圈等待 WaitForNotificationAsync()
     → 收到通知 → 加入 ConcurrentQueue<NotificationMessage>（最多保留 50 筆）
```

`NotificationMessage` 結構：
```json
{
  "receivedAt": "2026-04-15T06:45:00Z",
  "channel": "menu_update",
  "payload": { "item": "Oolong Tea", "price": 110, "available": true, "action": "INSERT" }
}
```

`NotificationStore`（Singleton）持有 `ConcurrentQueue`，同時被 BackgroundService 寫入、Controller 讀取。

---

## docker-compose 更新

新增兩個 service：

```yaml
hq-api:
  build: ./HqApi
  container_name: hq-api
  ports:
    - "5001:8080"
  environment:
    HQ_CONNECTION_STRING: "Host=hq-db;Port=5432;Database=coffee_shop;Username=hq_admin;Password=hq123"
  depends_on:
    hq-db:
      condition: service_healthy

branch-api:
  build: ./BranchApi
  container_name: branch-api
  ports:
    - "5002:8080"
  environment:
    BRANCH_CONNECTION_STRING: "Host=branch-db;Port=5432;Database=coffee_shop;Username=branch_admin;Password=branch123"
  depends_on:
    hq-db:
      condition: service_healthy
    branch-db:
      condition: service_healthy
```

存取位置：
- HqApi：`http://localhost:5001/api/menu`
- BranchApi：`http://localhost:5002/api/menu`、`http://localhost:5002/api/notifications`

---

## 資料流驗證步驟

1. `POST http://localhost:5001/api/menu` 新增菜單
2. 等約 1 秒
3. `GET http://localhost:5002/api/menu` 確認 Branch 已同步（無 cost 欄位）
4. `GET http://localhost:5002/api/notifications` 確認收到 NOTIFY 訊息

---

## 錯誤處理

| 情況 | 處理方式 |
|------|----------|
| DB 尚未就緒 | 啟動 retry loop（最多 10 次，間隔 3 秒） |
| Subscription 已存在 | 查詢 `pg_subscription` 後跳過 |
| Publication 已存在 | `DO $$` 冪等判斷後跳過 |
| HqApi 寫入失敗（DB 連線斷） | 回傳 500，不處理 retry（lab 範圍） |
| BranchApi LISTEN 連線斷 | BackgroundService 捕捉 exception，重新連線並 LISTEN |

---

## 不在範圍內（YAGNI）

- 驗證 / 授權（無 JWT、無 API Key）
- 分頁（GET /api/menu 回傳全部）
- BranchApi 寫入操作（read-only）
- StorePolicy、EmployeeSalary 的 API
- 前端 UI
