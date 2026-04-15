# Lab：PostgreSQL Logical Replication 實戰練習

> 透過咖啡連鎖店 HQ ↔ Branch 場景，練習 PostgreSQL Logical Replication 的選擇性複製、欄位過濾、NOTIFY 通知機制，以及 .NET 自動化部署。

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

**流程：**

```
發布端 PostgreSQL
├── 資料寫入 → WAL 記錄變更
├── Publication 定義要發布的表
└── Replication Slot 保存 WAL 給訂閱端

        ↕ TCP 連線（非同步）

訂閱端 PostgreSQL
├── Subscription 定義要訂閱的 publication
└── 收到變更 → 套用到本地表
```

**關鍵限制：**
- Logical Replication **不會自動建表**，訂閱端必須事先建好表結構
- 每張表只能有一個寫入端，避免衝突
- 需要設定 `wal_level=logical` 才能啟用

---

### PostgreSQL NOTIFY / LISTEN

**目的：** 讓應用程式即時知道「資料庫發生了變化」，不需要一直輪詢。

```
應用程式 A ─── INSERT/UPDATE ─── Trigger 觸發
                                      ↓
                              pg_notify('channel', 'payload')
                                      ↓
應用程式 B ─── LISTEN channel ─── 收到通知
```

| 概念 | 說明 |
|------|------|
| `LISTEN channel_name` | 應用程式宣告「要監聽這個頻道」 |
| `pg_notify(channel, payload)` | 發送通知到指定頻道，payload 是字串訊息 |
| Trigger | 資料表的自動觸發器，「當 INSERT/UPDATE 發生時，自動執行某 function」 |
| `ENABLE ALWAYS TRIGGER` | 讓 Trigger 在 replication 寫入時也觸發（預設不會） |

**限制：** NOTIFY 只在同一個 PostgreSQL instance 的 client 才能收到，不會跨機器傳遞。Branch 要收通知，需要在 Branch DB 上建 Trigger，由 replication 寫入觸發。

---

## 情境說明

**連鎖咖啡店：** 總部（HQ）管理菜單、定價、門市政策；分店（Branch）負責營運並參照。

### 資料流

```
HQ PostgreSQL
├── 菜單、定價、門市政策  ────── replication ──────  分店 PostgreSQL（read-only）
├── 人事薪資              ────── 不複製 ─────────────  （HQ 機密）
└── 菜單成本欄位          ────── 欄位過濾 ───────────  （分店看不到成本）
```

### 要練習的能力

1. 選擇特定資料表複製（排除人事薪資表）
2. 選擇特定欄位複製（排除菜單成本欄位）
3. Branch 設為 Read-only（應用程式層無法寫入）
4. NOTIFY：分店收到菜單變更時觸發通知

### 對應 AtheNAC 架構

| 咖啡店 Lab | AtheNAC 新架構 |
|------------|----------------|
| HQ pgAdmin 操作菜單 | 管理台 UI 操作政策設定 |
| `menu` 表（排除 cost 欄位） | 設定表（排除敏感欄位） |
| `store_policy` 表 | `admin_block_desired` 表 |
| `employee_salary` 不複製 | `event_log` 等 HQ 專用表不複製 |
| Branch DB 設為 read-only | Probe DB 設為 read-only |
| NOTIFY → POS 系統 | NOTIFY → PortWorker |
| 分店斷線 → 恢復自動 catch up | Probe 離線 → 恢復 replication catch up |

---

## 快速啟動

### 需求

- Docker Desktop（已啟動）
- .NET 8 SDK（本機開發用）

### 一鍵啟動

```bash
# 清環境（第一次跑或要重置）
docker-compose down -v

# 建置並啟動所有服務
docker-compose up --build
```

`hq-app` container 會自動依序執行：

1. 等待 HQ / Branch DB 就緒
2. EF Core Migration 建表（HQ + Branch）
3. 設定 Publication / Subscription / NOTIFY Trigger
4. 模擬 HQ 操作，並印出 Branch 同步結果

### 預期輸出

```
等待 DB 就緒...
HQ DB 已就緒
等待 Branch DB 就緒...
Branch DB 已就緒

=== Step 1: HQ Migration ===
HQ Migration 完成

=== Step 2: Branch Migration ===
Branch Migration 完成

=== Step 3: Replication 設定 ===
[Replication] 建立 Publication...
[Replication] Publication OK
[Replication] 建立 Subscription...
[Replication] Subscription 建立完成
[Replication] Subscription OK
[Replication] 建立 Branch NOTIFY Trigger...
[Replication] Trigger OK
[Replication] 設定 Branch read-only...
[Replication] Branch read-only OK

=== Step 4: HQ 模擬操作 ===

========== HQ 模擬操作開始 ==========

[HQ] 上架新品：Matcha Latte $160
[HQ] 調整價格：Americano $120 → $130
[HQ] 下架：Croissant

[等待] Replication 同步中...

[Branch] 同步後菜單：
  Americano             $130.00  available=True
  Latte                 $150.00  available=True
  Green Tea             $100.00  available=True
  Cheesecake            $180.00  available=True
  Croissant              $80.00  available=False
  Matcha Latte          $160.00  available=True

========== 模擬完成 ==========
```

---

## 服務清單

| 服務 | 說明 | 對外 Port |
|------|------|-----------|
| `hq-db` | HQ PostgreSQL（含完整欄位、人事薪資） | 5432 |
| `branch-db` | Branch PostgreSQL（read-only，無 cost 欄位） | 5433 |
| `hq-app` | .NET 自動化 console app | — |
| `pgadmin` | Web UI 管理工具 | 8080 |

---

## 手動操作（用 pgAdmin 驗證）

### 連線設定

開啟瀏覽器進 **http://localhost:8080**，登入 `admin@coffee.com` / `admin123`

新增兩個 Server：

| | HQ 總部 | Branch 分店 |
|-|---------|-------------|
| Host | `hq-db` | `branch-db` |
| Port | `5432` | `5432` |
| Database | `coffee_shop` | `coffee_shop` |
| Username | `hq_admin` | `branch_admin` |
| Password | `hq123` | `branch123` |

### 測試即時同步

在 **HQ 總部** 的 Query Tool 執行：

```sql
-- 上架新飲品
INSERT INTO menu (name, category, price, cost, is_available, updated_at)
VALUES ('Oolong Tea', 'tea', 110, 20, true, now());

-- 調整價格
UPDATE menu SET price = 140, updated_at = NOW() WHERE name = 'Americano';

-- 下架商品
UPDATE menu SET is_available = false, updated_at = NOW() WHERE name = 'Green Tea';
```

等約 1 秒後，在 **Branch 分店** 查詢：

```sql
SELECT name, price, is_available FROM menu ORDER BY id;
```

### 驗證機密資料不外洩

```sql
-- Branch 不應有 employee_salary 表
SELECT * FROM employee_salary;
-- 預期：ERROR: relation "employee_salary" does not exist

-- Branch 的 menu 表不應有 cost 欄位
SELECT cost FROM menu;
-- 預期：ERROR: column "cost" does not exist
```

### 驗證 Branch Read-only

```sql
-- 嘗試直接寫入 Branch，應被拒絕
INSERT INTO menu (name, category, price, is_available)
VALUES ('Test', 'coffee', 100, true);
-- 預期：ERROR: cannot execute INSERT in a read-only transaction
```

### 測試 NOTIFY（psql terminal）

開啟 Branch 的 psql terminal：

```bash
docker exec -it branch-db psql -U branch_admin -d coffee_shop
```

開始監聽：

```sql
LISTEN menu_update;
```

切換到 pgAdmin，在 **HQ 總部** 執行任意 INSERT/UPDATE，回到 terminal 輸入任一指令（如 `SELECT 1;`），即可看到：

```
Asynchronous notification "menu_update" with payload
"{"item":"Oolong Tea","price":110.00,"available":true,"action":"INSERT"}"
```

---

## 測試離線恢復

模擬分店斷線：

```bash
docker stop branch-db
```

HQ 繼續操作期間，Replication Slot 會保留 WAL 等待分店回來：

```sql
-- HQ 繼續更新
UPDATE menu SET price = 150, updated_at = NOW() WHERE name = 'Americano';
INSERT INTO menu (name, category, price, cost, is_available) VALUES
    ('Espresso', 'coffee', 100, 20, true);
```

恢復分店：

```bash
docker start branch-db
```

等幾秒後在 Branch 查詢，斷線期間的變更會自動 catch up：

```sql
SELECT name, price, is_available FROM menu ORDER BY id;
```

> **注意（實作維運）：** 分店長時間斷線而未清理的 Replication Slot，會導致 HQ 的 WAL 無限累積直到硬碟爆滿。實作上需監控 Slot 的狀態，並移除確定不再使用的 Slot：
> ```sql
> SELECT pg_drop_replication_slot('slot_name');
> ```

---

## 冪等性（重跑不報錯）

`hq-app` 的所有設定步驟均做冪等處理，重新 `docker-compose up` 不需要 `down -v`：

- Publication 已存在 → 跳過
- Subscription 已存在 → 跳過
- Trigger → `CREATE OR REPLACE` 自動覆蓋

---

## 清除環境

```bash
docker-compose down -v
```

`-v` 會同時清除 volume（資料庫資料），下次啟動為全新狀態。

---

## 專案結構

```
lab-postgres-replication/
├── docker-compose.yml          # 環境定義（hq-db、branch-db、hq-app、pgadmin）
├── HqApp/
│   ├── Dockerfile              # Multi-stage build
│   ├── HqApp.csproj
│   ├── Program.cs              # 進入點（Migration → Replication → Simulation）
│   ├── Models/
│   │   ├── Menu.cs             # HQ 菜單（含 cost）
│   │   ├── StorePolicy.cs      # 門市政策
│   │   ├── EmployeeSalary.cs   # 人事薪資（HQ 專用）
│   │   ├── BranchMenu.cs       # Branch 菜單（不含 cost）
│   │   └── BranchStorePolicy.cs
│   ├── Data/
│   │   ├── HqDbContext.cs      # HQ DB context（含 seed data）
│   │   ├── BranchDbContext.cs  # Branch DB context
│   │   └── Migrations/
│   │       ├── Hq/             # EF Core Migration for HQ
│   │       └── Branch/         # EF Core Migration for Branch
│   └── Services/
│       ├── ReplicationSetupService.cs   # 建 Publication/Subscription/Trigger（冪等）
│       └── HqSimulationService.cs       # 模擬 HQ 操作並印出 Branch 結果
└── docs/
    └── superpowers/
        └── plans/
            └── 2026-04-15-hqapp-dotnet.md
```
