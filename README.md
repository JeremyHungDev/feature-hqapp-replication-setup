# Lab：PostgreSQL Logical Replication 實戰練習

> 透過咖啡連鎖店 HQ ↔ Branch 場景，練習 PostgreSQL Logical Replication 的選擇性複製、欄位過濾、NOTIFY 通知機制，以及 .NET 獨立微服務 (HqApi + BranchApi) 自動化部署。

---

## 前置知識

### PostgreSQL Logical Replication

**目的：** 讓一台 PostgreSQL 的資料自動同步到另一台，且可以只選擇特定表、特定欄位。

**與 Physical Replication 的差異：**

| | Physical | Logical |
|--|----------|---------|
| 複製單位 | 整個 DB cluster（全部） | 可選擇特定表、特定欄位 |
| Replica 可寫入 | 不行（完全 read-only） | 可以（未被訂閱的表可以寫） |
| 過濾 | 不行 | 可以（每張表有一個 owner） |

**核心概念：**

| 概念 | 角色 | 說明 |
|------|------|------|
| **Publication** | 發布端（資料來源） | 定義「要發布哪些表/欄位」 |
| **Subscription** | 訂閱端（資料接收） | 定義「要訂閱誰的 publication」 |
| **WAL** | 底層機制 | PostgreSQL 的交易日誌，replication 靠 WAL 來同步變更 |
| **Replication Slot** | 底層機制 | 確保 WAL 不在訂閱端讀完前被清除 |

---

### PostgreSQL NOTIFY / LISTEN

**目的：** 讓應用程式即時知道「資料庫發生了變化」，不需要一直輪詢。

```
應用程式 A(HqApi) ─── INSERT/UPDATE ─── Trigger 觸發
                                              ↓
                                      pg_notify('channel', 'payload')
                                              ↓
應用程式 B(BranchApi) ─── LISTEN channel ─── 收到通知
```

| 概念 | 說明 |
|------|------|
| `LISTEN channel_name` | 應用程式宣告「要監聽這個頻道」 |
| `pg_notify(channel, payload)` | 發送通知到指定頻道，payload 是 JSON 格式的訊息 |
| Trigger | 資料表的自動觸發器，「當 INSERT/UPDATE 發生時，自動執行」 |

---

## 情境說明

**連鎖咖啡店：** 總部（HQ）管理菜單、定價；分店（Branch）負責營運並接收即時推播。

### 資料流

```
HQ PostgreSQL
├── 菜單、定價        ────── replication ──────  分店 PostgreSQL（read-only）
└── 菜單成本欄位      ────── 欄位過濾 ───────────  （分店看不到成本）
```

---

## 快速啟動與 API 驗證

### 需求

- Docker Desktop（已啟動）
- 任何可用於打 HTTP API 的工具（PowerShell, cURL, Postman 等）

### 一鍵啟動

```bash
# 清環境（第一次跑或要重置）
docker-compose down -v

# 建置並在背景啟動所有微服務
docker-compose up -d --build
```

Docker Compose 將為您自動依序啟動環境，並執行 EF Core Migration 以及 Replication / Subscription / Trigger 的建置。

### 動手玩：API 即時驗證
待所有服務順利啟動完成後，您可以打開終端機 (PowerShell 或 Command Prompt) 開始透過 API 實際體驗：

**1. 查詢總部菜單（能看到原始的 `cost` 成本欄位）**
```powershell
Invoke-RestMethod -Uri http://localhost:5001/api/menu
```

**2. 查詢分店菜單（看見成功複製的資料，但隱藏了 `cost` 欄位）**
```powershell
Invoke-RestMethod -Uri http://localhost:5002/api/menu
```

**3. 在總部新增一筆菜單（觸發 INSERT 通知）**
```powershell
Invoke-RestMethod -Uri http://localhost:5001/api/menu -Method POST -ContentType "application/json" -Body '{"name": "Oolong Tea", "category": "tea", "price": 110, "cost": 20}'
```

**4. 在總部修改菜單價格（觸發 UPDATE 通知）**
```powershell
Invoke-RestMethod -Uri http://localhost:5001/api/menu/2 -Method PUT -ContentType "application/json" -Body '{"price": 170}'
```

**5. 向分店查詢收到的 NOTIFY 通知**
```powershell
Invoke-RestMethod -Uri http://localhost:5002/api/notifications
```

預期回應（最新的在最後）：
```json
[
  {
    "receivedAt": "2026-04-15T07:00:01Z",
    "channel": "menu_update",
    "payload": "{\"item\":\"Oolong Tea\",\"price\":110.00,\"available\":true,\"action\":\"INSERT\"}"
  },
  {
    "receivedAt": "2026-04-15T07:00:05Z",
    "channel": "menu_update",
    "payload": "{\"item\":\"Latte\",\"price\":170.00,\"available\":true,\"action\":\"UPDATE\"}"
  }
]
```

> `action` 欄位會顯示 `INSERT` 或 `UPDATE`，代表 HQ 觸發的操作類型。

**6. 用 docker logs 即時監看通知（可同時開著）**

```bash
docker logs -f branch-api
```

當步驟 3、4 執行後，log 中會即時出現：
```
[NOTIFY] menu_update: {"item":"Oolong Tea","price":110.00,"available":true,"action":"INSERT"}
[NOTIFY] menu_update: {"item":"Latte","price":170.00,"available":true,"action":"UPDATE"}
```

**NOTIFY 完整流程：**
```
HqApi POST/PUT → hq-db 寫入
    → Logical Replication → branch-db 套用
    → Trigger (trg_menu_notify) 觸發
    → pg_notify('menu_update', payload)
    → BranchApi BackgroundService (LISTEN) 收到
    → 存入記憶體 NotificationStore
    → GET /api/notifications 可查詢
```

---

## 服務清單

| 服務 | 說明 | 對外 Port |
|------|------|-----------|
| `hq-db` | HQ PostgreSQL（含完整欄位） | 5432 |
| `branch-db` | Branch PostgreSQL（read-only，無 cost 欄位） | 5433 |
| `hq-api` | 總部 API：負責新增/修改資料、建立發布端 | 5001 |
| `branch-api` | 分店 API：背景常駐長連接 `LISTEN` 推播通知 | 5002 |
| `pgadmin` | Web UI 管理工具 (admin@coffee.com / admin123) | 8080 |

---

## 測試離線恢復 (Catch up)

1. 模擬分店斷線：
```bash
docker stop branch-db
```
2. 此時繼續對 `hq-api` 進行資料的 POST 或 PUT 寫入。因為 HQ 有保留 Replication Slot，傳遞序列檔案 (WAL) 會替我們留著等待分店。
3. 恢復分店：
```bash
docker start branch-db
```
4. 呼叫 `BranchApi` 端點，斷線期間的變更會全部自動補齊！

---

## 專案結構

此專案為微服務架構，包含兩個獨立運行的 ASP.NET Core 應用程式：

```
lab-postgres-replication/
├── docker-compose.yml          # 環境服務定義（PG x 2, APIs x 2, pgAdmin x 1）
├── HqApi/                      # 總部微服務
│   ├── Controllers/
│   │   └── MenuController.cs              # 提供對外寫入與操作介面
│   ├── Models/Menu.cs                     # 帶有 Cost 欄位的核心模型
│   ├── Data/HqDbContext.cs                # EF Core Context 與 預設資料
│   └── Services/ReplicationPublisherService.cs  # 負責建立 pg_publication (剔除成本)
│
└── BranchApi/                  # 分店微服務
    ├── Controllers/
    │   ├── MenuController.cs              # 受讀取保護的資料查詢介面
    │   └── NotificationsController.cs     # 查詢最近的資料庫事件通知
    ├── Models/BranchMenu.cs               # 被閹割 Cost 欄位的模型
    ├── Data/BranchDbContext.cs            # Branch 節點 Context
    └── Services/
        ├── ReplicationSubscriberService.cs   # 負責向總部建立 pg_subscription 並寫入 Trigger
        ├── MenuNotificationListener.cs       # 【核心】負責長連接 LISTEN menu_update 的背景服務
        └── NotificationStore.cs              # 單例記憶體，用於推播暫存
```
