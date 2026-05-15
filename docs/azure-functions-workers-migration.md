# Azure Functions 背景工作者遷移（僅 Api 執行面）

本文件為 **feature/azure-functions-workers** 分支的實作檢核表：將 `NeighborGoods.Api` 內四個 `IHostedService` 改由 **Azure Functions（Linux Flex Consumption、isolated .NET）** 的 Timer 觸發同一套「單次執行」邏輯；舊 **`NeighborGoods.Web`（MVC）** 已自 repo 移除，不再部署。

## 原則

- **納入**：`NeighborGoods.Api`、`NeighborGoods.Functions`、`NeighborGoods.Data`、`NeighborGoods.Notifications`、`NeighborGoods.Workers`、`NeighborGoods.Messaging`（SignalR 即時推播與訊息 DTO 共用）。
- **不納入**：舊 MVC 全站（已刪除；考古請用 Git 歷史）。
- **解耦**：Api 不參考 Functions；Functions 不參考 Api WebHost；共用透過上述類別庫。
- **雙跑風險**：正式上線前僅在一側啟用排程（本變更預設 **Api 已移除 HostedService**，由 Functions 負責）。

## Git 流程（摘要）

1. `git fetch origin`
2. `git checkout main` && `git pull`
3. `git checkout -b feature/azure-functions-workers`（已存在則略過）
4. 本文件所列變更僅在此分支提交；完成後開 PR 合併 `main`。

捨棄分支重做時請參考需求方提供的 `git reset --hard` / `git clean` 流程（`git clean` 前務必確認未追蹤檔不需保留）。

---

## 階段 0：文件與分支

| 步驟 | 動作 | 驗證 |
|------|------|------|
| 0.1 | 確認已切換 `feature/azure-functions-workers` | `git branch --show-current` |
| 0.2 | 本檔案置於 `docs/azure-functions-workers-migration.md` | 檔案存在 |

---

## 階段 1：舊 MVC（已自 repo 移除）

| 步驟 | 動作 | 驗證 |
|------|------|------|
| 1.1 | 自 `NeighborGoods.sln` 移除 `NeighborGoods.Web`，並刪除 repo 內舊 MVC 原始碼 | `dotnet sln list` 不含 Web；repo 根目錄無舊 MVC 之 `legacy/` |
| 1.2 | 更新根目錄 `README.md` 與本文件，不再描述封存之 `legacy/NeighborGoods.Web` | 手動閱讀 |

**驗證指令：**

```powershell
dotnet build NeighborGoods.sln -c Release
```

---

## 階段 2：NeighborGoods.Data（DbContext、實體、Migrations）

| 步驟 | 動作 | 驗證 |
|------|------|------|
| 2.1 | 新增 `NeighborGoods.Data`（`net10.0`）與 `Microsoft.EntityFrameworkCore.SqlServer` | csproj 可還原 |
| 2.2 | 將 `Shared/Persistence`（含 `LegacyEntities`）移至 `NeighborGoods.Data`；命名空間改為 `NeighborGoods.Data` / `NeighborGoods.Data.LegacyEntities` | 無孤兒檔 |
| 2.3 | 將 EF 實體（原 `Features/Listing` 之 Listing / Lookup 實體等）移至 `NeighborGoods.Data.Listings` | DbContext 可編譯 |
| 2.4 | `DatabaseHealthCheck` 保留在 **Api**（Infrastructure），避免 Data 依賴 HealthChecks | Api 專案內有實作 |
| 2.5 | 將 `Migrations` 移至 `NeighborGoods.Data`；更新 `ModelSnapshot` / Designer 內之 CLR 型別字串前綴 | `dotnet build` |
| 2.6 | `NeighborGoods.Api` 加入 `ProjectReference` 至 `NeighborGoods.Data`；全方案取代舊 `NeighborGoods.Api.Shared.Persistence` 等 using | Api 編譯通過 |

**驗證指令：**

```powershell
dotnet build NeighborGoods.sln -c Release
```

**後續 EF CLI（選用）：** 新增 migration 時建議：

```powershell
dotnet ef migrations add <Name> --project NeighborGoods.Data --startup-project NeighborGoods.Api
```

---

## 階段 3：NeighborGoods.Notifications

| 步驟 | 動作 | 驗證 |
|------|------|------|
| 3.1 | 新增 `NeighborGoods.Notifications`，移入原 `Shared/Notifications` 與寄信／LINE 相關實作 | 命名空間 `NeighborGoods.Notifications` |
| 3.2 | 將 `LineFlexMessageBuilder` 移入（或子命名空間），依賴 `LineMessagingOptions` | 無循環參考 |
| 3.3 | `LinePushPolicyService` 改參考 `NeighborGoods.Data.LegacyEntities` | `dotnet build` |

**驗證指令：**

```powershell
dotnet build NeighborGoods.sln -c Release
```

---

## 階段 4：NeighborGoods.Messaging（SignalR + 系統訊息推播）

| 步驟 | 動作 | 驗證 |
|------|------|------|
| 4.1 | 新增 `NeighborGoods.Messaging`（`FrameworkReference` Microsoft.AspNetCore.App 或等價套件） | 可編譯 |
| 4.2 | 移入 `MessageItemDto`、`MessageHub`、`SystemMessageRealtimePublisher`（由原 `PurchaseRequestService` 之推播邏輯抽出） | Api 仍通過 DI 註冊 Hub |
| 4.3 | `PurchaseRequestService` 改依賴 `ISystemMessageRealtimePublisher`，排程邏輯移至 `NeighborGoods.Workers` 之 `IPurchaseRequestScheduledOperations` | Api 僅委派；Functions 與 Api 共用 Workers |

**驗證指令：**

```powershell
dotnet build NeighborGoods.sln -c Release
```

---

## 階段 5：NeighborGoods.Workers（單次執行邏輯）

| 步驟 | 動作 | 驗證 |
|------|------|------|
| 5.1 | 新增 `NeighborGoods.Workers`，參考 Data、Notifications、Messaging | 無參考 Api |
| 5.2 | 移入／實作 `QuickResponderEvaluationService`、`PurchaseRequestScheduledOperations`（逾時／提醒）、`UnreadMessageEmailNotificationJob`、`LinePreferencePushJob` | 不依賴 `BackgroundService` |
| 5.3 | 提供 `IServiceCollection` 擴充方法（例如 `AddNeighborGoodsWorkerServices`）供 Api 與 Functions 註冊相同後端服務子集合 | `Program.cs` 可呼叫 |

**驗證指令：**

```powershell
dotnet build NeighborGoods.sln -c Release
```

---

## 階段 6：NeighborGoods.Functions（Timer）

| 步驟 | 動作 | 驗證 |
|------|------|------|
| 6.1 | 新增 **isolated** 專案 `NeighborGoods.Functions`（`net10.0`），套件：`Microsoft.Azure.Functions.Worker`、`Worker.Sdk`、`Worker.Extensions.Timer` | `func` 或 `dotnet build` 成功 |
| 6.2 | 設定 `host.json`、`local.settings.json`（本機）；應用程式設定與 Api 對齊（`ConnectionStrings:DefaultConnection`、`Email`、`Line` 等） | 本機可啟動（若已安裝 Azure Functions Core Tools） |
| 6.3 | 四個 Timer：`UnreadMessageEmail`（每分鐘）、`LinePreferencePush`（≥5 分，可由設定驅動）、`PurchaseRequestExpiration`（每 10 分）、`QuickResponderBadge`（每日 UTC 02:00） | Cron 與原間隔一致 |
| 6.4 | 將 `NeighborGoods.Functions` 加入 solution | `dotnet sln list` |

**建議 NCRONTAB（6 欄位）：**

- 未讀信：`0 */1 * * * *`（每分鐘）
- LINE 偏好推播：預設 `0 */5 * * * *`（實際間隔仍受程式內 `LineMessagingOptions` 節流）
- 購買請求：`0 */10 * * * *`
- 快速回應徽章：`0 0 2 * * *`（每日 UTC 02:00）

**驗證指令：**

```powershell
dotnet build NeighborGoods.Functions/NeighborGoods.Functions.csproj -c Release
```

---

## 階段 7：Api 移除 HostedService

| 步驟 | 動作 | 驗證 |
|------|------|------|
| 7.1 | 自 `NeighborGoods.Api/Program.cs` 移除四個 `AddHostedService<>` | 無 `IHostedService` 註冊該四項 |
| 7.2 | 刪除或清空原 `*Worker.cs`（邏輯已於 Workers / Functions） | 專案無重複排程迴圈 |
| 7.3 | 更新 `HostedServicesRegistrationTests`（改驗證「未註冊」或改測 Functions 專案） | 測試通過 |

**驗證指令：**

```powershell
dotnet test NeighborGoods.Api.Tests/NeighborGoods.Api.Tests.csproj -c Release --filter "FullyQualifiedName!~EndpointsTests"
```

---

## 階段 8：CI/CD

| 步驟 | 動作 | 驗證 |
|------|------|------|
| 8.1 | 更新 `.github/workflows/backend_ci.yml`：`paths` 含新專案目錄；`dotnet format` / `build` / `test` 涵蓋 solution 或各 csproj | PR 上 CI 綠 |
| 8.2 | **IaC**：`deploy.bicep` 於 `deployFunctionsApp=true` 時部署 [functionapp-linux-flex-consumption.bicep](../infra/bicep/modules/functionapp-linux-flex-consumption.bicep)（**Linux Flex Consumption、FC1**、`functionAppConfig`、dotnet-isolated、專用儲存體 + **User-assigned managed identity** 存取 Blob/Queue/Table、`deployments` 容器供 zip 套件）。預設 Function App 名稱為 `${namePrefix}-${environmentName}-func-flex`（參數 `functionsResourceNameSuffix`，預設 `-flex`；設為空字串則名稱為 `${namePrefix}-${environmentName}-func`，須先移除舊站以免名稱衝突）。**Linux Consumption（Y1）無法原地升級**，新站與舊站可並行於同一資源群組驗證後再改 CD 變數。**CD**：手動執行 [Backend CD (Azure Functions)](../.github/workflows/backend_cd_functions.yml)；Repository variables：`AZURE_RESOURCE_GROUP`、`AZURE_FUNCTION_APP_NAME`（須與目前 IaC 輸出的名稱一致，預設含 `-flex` 後綴）。OIDC secrets 同 Container App CD。上線前請確認區域支援 Flex：`az functionapp list-flexconsumption-locations`。 | IaC 部署成功；CD `config-zip` 發佈成功 |

---

## Azure Functions 託管注意事項（Flex Consumption 摘要）

- 方案為 **Flex Consumption（FC1）**；與已淘汰路線的 **Linux Consumption（Y1）** 不同，後者不支援 .NET 10 後續堆疊，請勿再部署 Y1 範本。
- **VNET／私人端點**：Flex 相較傳統 Consumption 對網路整合較完整；若 SQL 僅限私人存取，請對照 [Flex 網路](https://learn.microsoft.com/azure/azure-functions/flex-consumption-how-to) 與防火牆設定。
- **部署槽、自管 TLS 憑證**：Flex 上限制請見 [遷移指南](https://learn.microsoft.com/azure/azure-functions/migrate-plan-consumption-to-flex)；本專案目前僅 Timer 觸發，無 Blob 輪詢觸發，遷移負擔較低。
- 未讀信每分鐘觸發執行次數較高；若成本或延遲有壓力可改為 2～5 分鐘並調整產品容忍度。

---

## 完成定義（DoD）

- [x] `dotnet build NeighborGoods.sln -c Release` 通過  
- [x] `dotnet test`（排除長時間 EndpointsTests）通過  
- [x] Api 已移除四個 `IHostedService` 註冊  
- [x] `NeighborGoods.Functions` 與四個 Timer（isolated .NET、`Microsoft.Azure.Functions.Worker` 2.51+、`Microsoft.Azure.Functions.Worker.Sdk` 2.0.6+）
- [x] 舊 `NeighborGoods.Web` 已自 repo 移除；README／本文件已更新  

## 本分支已實作摘要

- **舊 MVC**：`NeighborGoods.Web` 已自 repo 刪除；`NeighborGoods.sln` 不再含該專案；根 `README.md` 已更新。  
- **NeighborGoods.Data**：EF `NeighborGoodsDbContext`、`LegacyEntities`、Listing 實體、`PurchaseRequestConstants`、`Migrations` 自 Api 移出；Api 以 `ProjectReference` 參考。  
- **NeighborGoods.Notifications**：原 `Shared/Notifications` 與 `LineFlexMessageBuilder`、`LineFlexDtos`（自 `LineMenuQueryService` 抽出之 record）。  
- **NeighborGoods.Messaging**：`MessageHub`、`MessageItemDto`、`ISystemMessageRealtimePublisher`／`SystemMessageRealtimePublisher`；Api 註冊推播實作。  
- **NeighborGoods.Workers**：`PurchaseRequestScheduledOperations`（逾時／提醒）、`QuickResponderEvaluationService`、`UnreadMessageEmailNotificationJob`、`LinePreferencePushJob`；`AddNeighborGoodsWorkerJobs`（Api）／`AddNeighborGoodsWorkerHosting`（Functions）。  
- **NeighborGoods.Functions**：`ScheduledTriggers` 四個 Timer（與文件 NCRONTAB 一致）；`Program` 呼叫 `AddNeighborGoodsWorkerHosting`；`local.settings.json` 範本。  
- **Api**：移除四個 `AddHostedService` 與舊 `*Worker.cs`；`PurchaseRequestService` 改委派 `IPurchaseRequestScheduledOperations` 並統一走 `ISystemMessageRealtimePublisher`。  
- **`PurchaseRequestStatus`**：移至 `NeighborGoods.Data.PurchaseRequests`。  
- **CI**：`backend_ci.yml` 改為 `dotnet restore/format/build/test` 針對整個 solution，並納入新專案 paths；`dotnet-version` 改為 `10.0.x`。  

## 已知議題（請於合併前處理）

- **zip 部署失敗 `InvalidAppSettingsException: SCM_DO_BUILD_DURING_DEPLOYMENT`**：Linux **Flex Consumption** 不支援此應用程式設定（即使值為 `false`）。若 IaC 曾寫入，請先刪除再重新 `config-zip`：
  ```powershell
  az functionapp config appsettings delete `
    --resource-group <resource-group> `
    --name neighborgoods-prod-func-flex `
    --setting-names SCM_DO_BUILD_DURING_DEPLOYMENT
  ```
  `functionapp-linux-flex-consumption.bicep` 已移除該設定；既有資源需手動刪除或重跑 IaC 後再部署。
- **本機執行 Functions**：需安裝 [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) 並設定 `local.settings.json` 內 `ConnectionStrings:DefaultConnection`、（選用）`Azure:SignalR:ConnectionString` 與 Email／Line 區段與 Api 一致。  
- **`PurchaseRequestService.cs` 與 `PurchaseRequestScheduledOperations.cs` 之繁體字串**：請維持 UTF-8；系統訊息字首須與 `SystemMessageRealtimePublisher` 內 `StartsWith("[系統發送]")` 一致。

