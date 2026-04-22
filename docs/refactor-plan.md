# NeighborGoods 單一重構計畫

> 目的：將原本分散在多份文件的重點收斂成一份可執行計畫，作為目前唯一維護文件。

## 1. 目標與範圍

- 開發模式：單人開發。
- 環境策略：本地開發 + 單一 Azure 正式環境（暫不設 staging）。
- 技術方向：現有 MVC 穩定運行下，漸進式演進到 API + React 分離架構。
- 交付原則：持續可部署優先，不做大爆炸重寫。

## 2. 成功標準

- 核心流程無退化：登入、刊登、查詢、訊息、圖片上傳可用。
- `NeighborGoods.Api` 與資料庫連線可穩定運作（不誤用 in-memory fallback）。
- `NeighborGoods.Api` 可同時支援帳密登入與 LINE OAuth，統一簽發 JWT（Access/Refresh）。
- Listing 寫入端點強制授權；`PUT` / `DELETE` / 狀態 `PATCH` 僅限該筆商品之賣家本人（否則 `403`、`LISTING_ACCESS_DENIED`）；`POST` 上架另須通過 `EmailConfirmed`。
- 每次上線有一致檢查流程（build、設定鍵、smoke test、回滾）。

## 3. 現況摘要（2026-04，已更新）

- API 目前聚焦 `Listing` 垂直切片，已完成 CRUD 路由與統一回應契約。
- Listing 資料存取已改為 EF Core 單一路徑（LINQ），不再使用 Dapper 與讀寫分離。
- Listings 查詢邏輯：`Status in (Active, Reserved)`、`q` 搜尋 `Title/Description`、`CreatedAt DESC`、分頁。
- `DefaultConnection` 已改為必填，未設定時啟動失敗（避免誤用 in-memory fallback）。
- 已完成 API Auth 導入：
  - `POST /api/v1/auth/login`（帳密登入）
  - `POST /api/v1/auth/refresh`（換發）
  - `POST /api/v1/auth/revoke`（撤銷）
  - `GET /api/v1/auth/line/login`、`GET /api/v1/auth/line/callback`（LINE OAuth）
- Listing 寫入流程已移除 `SystemSellerProvider`，改為 `CurrentUser` claims 取得 `SellerId`。
- 上架已強制檢查 `EmailConfirmed=true`，未通過回 `403`。
- Listing 更新、刪除與狀態變更已比對 `SellerId` 與目前 JWT 使用者，非賣家回 `403`（`LISTING_ACCESS_DENIED`）。
- 商品種類（`Category`）已改為資料表 `ListingCategories`（含 seed、`Listings.Category` FK）；`POST`/`PUT` 會驗證為作用中分類；查詢顯示名稱讀 DB。
- 品況、社宅、面交地點已改為 `ListingConditions` / `ListingResidences` / `ListingPickupLocations`（含 seed、對應 `Listings` FK）；`POST`/`PUT` 需帶 `pickupLocationCode` 並驗證；查詢顯示名稱讀 DB；已移除 `ListingLookupCatalog`。
- 已提供 `GET /api/v1/lookups/categories|conditions|residences|pickup-locations`（不需登入）。
- 已提供對話／訊息 API（`Features/Messaging`）：REST 開聊、列表、訊息分頁、發訊、已讀；SignalR `/hubs/messages` 推播 `ReceiveMessage`；`Messages.Content` 欄位長度已擴至 1000（migration `ExpandMessagesContentMaxLength`）。
- 已提供帳號 API（`Features/Account`）：註冊寄碼／註冊、刊登前 Email 驗證寄碼／驗證、`GET/PATCH /api/v1/account/me`、LINE 官方帳號綁定（start/status/confirm/unbind）；驗證碼改由 DB `EmailVerificationChallenges` 管理（migration `AddEmailVerificationChallenges`）。
- 整合測試已擴充為 auth + listing + messaging + account 共 67 個案例，全綠。
- API 結構已重排為 Pure Feature：
  - `Features/Listing`（endpoint/service/repository/contracts 共置）
  - `Features/Lookups`（categories / conditions / residences / pickup-locations）
  - `Features/Auth`（endpoints/services/contracts/configuration）
  - `Features/System`（health/ping）
  - `Shared`（API 契約、共用型別、DbContext、CurrentUser security）

## 3.1 目前目錄策略（Pure Feature）

- `Features/Listing`
  - `ListingEndpoints.cs`
  - `Services/ListingQueryService.cs`
  - `Services/ListingCommandService.cs`
  - `Services/ListingStatusService.cs`
  - `ListingStatus.cs` / `ListingStatusRules.cs`
  - `Listing.cs` / `ListingCategory.cs` / `ListingCondition.cs` / `ListingResidence.cs` / `ListingPickupLocation.cs` / `ListingSummary.cs`
  - `Contracts/Requests/*`
  - `Contracts/Responses/*`
- `Features/Auth`
  - `AuthEndpoints.cs`
  - `Services/TokenService.cs`
  - `Services/PasswordAuthService.cs`
  - `Services/LineOAuthClient.cs`
  - `Services/LineOAuthStateStore.cs`
  - `Contracts/Requests/*`
  - `Contracts/Responses/*`
  - `Configuration/JwtOptions.cs`
  - `Configuration/LineOAuthOptions.cs`
- `Features/Lookups`
  - `LookupEndpoints.cs`
- `Features/System`
  - `SystemEndpoints.cs`
- `Features/Messaging`
  - `MessagingEndpoints.cs`、`MessageHub.cs`、`Services/MessagingQueryService.cs`、`Services/MessagingCommandService.cs`、`Contracts/*`
- `Features/Account`
  - `AccountEndpoints.cs`、`Services/AccountRegistrationService.cs`、`Services/AccountEmailVerificationService.cs`、`Services/AccountProfileService.cs`、`Services/AccountLineBindingService.cs`、`Contracts/*`
- `Features/Integrations/Line`
  - `LineWebhookEndpoints.cs`、`Services/LineWebhookService.cs`
- `Shared`
  - `ApiContracts/*`
  - `Contracts/*`
  - `Notifications/*`
  - `Persistence/NeighborGoodsDbContext.cs`
  - `Security/ICurrentUserContext.cs`
  - `Security/HttpCurrentUserContext.cs`

## 3.2 API / Web 邊界守則（重構期間）

- 定位原則：`NeighborGoods.Web` 僅作為商業流程與舊行為的參考來源，不列入正式依賴鏈。
- 依賴原則：`NeighborGoods.Api` 與 `NeighborGoods.Web` 之間禁止專案參考（`ProjectReference`）與程式碼相依（namespace/type 互引）。
- 實作原則：API 僅依據自身 domain、contract、資料模型實作商業邏輯；不得複製或綁定 MVC ViewModel / UI 專用 enum / controller 邏輯。
- 驗證原則：凡從 Web 參考而來的流程，需先轉成 API use-case 與測試案例（整合測試優先），再進入 API 實作。
- 契約原則：API 對外行為以 `/api/v1` 契約為唯一準則；Web 既有行為若與 API 契約衝突，以 API 契約為準並記錄差異。
- 退場原則：Web 移除前，需確認核心流程（登入、刊登、查詢、訊息、圖片）可由 API 驗證通過，且無任何 API 程式碼依賴 Web 內容。

## 4. 單一執行路線圖

### Phase 0 - 基線凍結（1-2 天）

- 建立穩定基線（tag 或明確 commit）。
- 固化 smoke test 清單：登入、刊登、查詢、訊息、圖片。
- 確認開發環境與正式環境必要設定鍵。

### Phase 1 - API 穩定化（2-4 天）

- 補齊 API 契約一致性（成功/錯誤回應格式、狀態碼語意）。
- 補 `GET /api/v1/listings/{id}` 與 CRUD 基礎路由。
- 明確化 DB 連線策略：開發環境與測試皆以 `DefaultConnection` 為顯式設定來源。

#### Phase 1B - Listing CRUD（已完成）

- `GET /api/v1/listings`
- `GET /api/v1/listings/{id}`
- `POST /api/v1/listings`
- `PUT /api/v1/listings/{id}`
- `DELETE /api/v1/listings/{id}`
- 最小 API 測試：5 個案例全綠（含成功與 NotFound）。
- 測試改為使用 `Testcontainers.MsSql`，每次測試啟動獨立 SQL 容器，避免誤連正式資料庫。

#### Phase 1C - Migration 正式化（已完成）

- 已建立初始 migration：`InitListingSchema`（`NeighborGoods.Api/Migrations`）。
- API 啟動策略：`Program.cs` 不在啟動期執行 `Database.Migrate()`，避免正式環境隱式變更 schema。
- 測試初始化策略：`NeighborGoods.Api.Tests` 使用 `Database.Migrate()`，確保測試 schema 演進與正式流程一致。

#### Phase 1D - Listing 狀態流與 Use-case 拆分（已完成）

- Listing 服務拆分為 Query / Command / Status 三個 use-case 服務，避免單一 service 持續膨脹。
- 狀態流 v1：
  - `Active(0) -> Reserved | Sold | Archived`
  - `Reserved(1) -> Active | Sold | Archived`
  - `Sold(2) -> Archived`
  - `Archived(3) -> Active`
- 新增狀態路由：
  - `PATCH /api/v1/listings/{id}/reserve`
  - `PATCH /api/v1/listings/{id}/activate`
  - `PATCH /api/v1/listings/{id}/sold`
  - `PATCH /api/v1/listings/{id}/archive`
- 非法狀態轉換回傳 `LISTING_INVALID_STATUS_TRANSITION`，避免前端誤判為一般驗證錯誤。

#### Phase 1E - API Auth 與上架授權（已完成）

- 已導入 JWT Bearer 驗證與授權中介層（`AddAuthentication().AddJwtBearer(...)` + `UseAuthentication/UseAuthorization`）。
- 已提供 API Auth 路由：`login/refresh/revoke/line-login/line-callback`。
- Listing 寫入端點（`POST/PUT/DELETE/PATCH`）皆要求授權；`PUT`/`DELETE`/狀態 `PATCH` 僅賣家本人。
- 上架流程由 `CurrentUser` 取得 `SellerId`，不再使用系統預設賣家。
- 上架前檢查 `EmailConfirmed`，未驗證回 `403`（`EMAIL_NOT_CONFIRMED`）。
- 測試覆蓋 auth（帳密、refresh/revoke、LINE callback）與授權情境（401/403/成功、非賣家寫入、無效分類／面交、lookups）、messaging（對話建立、發訊、已讀、權限）與 account（寄碼、註冊、刊登前 Email 驗證、me）等案例。

### Migration 操作規範（開發 / 正式 / 測試）

- 開發新增 schema 變更：
  - `dotnet ef migrations add <MigrationName> --project NeighborGoods.Api --startup-project NeighborGoods.Api`
  - `dotnet ef database update --project NeighborGoods.Api --startup-project NeighborGoods.Api`
  - macOS／Linux：請先設定 `ConnectionStrings__DefaultConnection`（指向本機 Docker SQL、Azure SQL 等），再執行上述指令。
- 正式環境套用：
  - 由手動或 CI 執行 `dotnet ef database update`（不在 API 啟動時自動套用）。
  - 套用前後需保留 migration 名稱與執行紀錄，納入部署檢查。
- 測試環境套用：
  - `ListingEndpointsTests` 由 `Testcontainers.MsSql` 啟動容器，並以 `Database.Migrate()` 建立/對齊 schema。
  - 禁止在測試流程使用正式資料庫連線字串；測試初始化只允許容器提供的連線。
  - 資料重置策略為清理測試資料表 + seed，不使用 `EnsureDeleted()`。

### Phase 2 - 主檔去硬編碼（3-5 天）

- 將 `Category/Condition/Residence/PickupLocation` 轉為資料表維護。
- **進度**：`Category` / `Condition` / `Residence` / `PickupLocation` 皆已改為資料表、seed、FK、lookup API 與寫入驗證；`ListingLookupCatalog` 已移除。Web MVC 仍暫以 enum 對應數值，後續可改讀 API 或同庫。
- 查詢與表單改由 Lookup table 提供，不再依賴 enum 硬編碼。
- 採雙軌遷移：先讀取切換，再寫入切換，最後移除舊欄位。

### Phase 3 - DDD 漸進重構（1-2 週）

- 目標：在 Pure Feature 下持續保持邏輯邊界（endpoint 薄層、service 聚合業務流程、repository 負責 I/O）。
- 先強化 Listing 模組：查詢優化、狀態流、驗證規則收斂。
- 若未來功能檔案數超過可讀門檻（如單 feature > 10~15 檔），再做 feature 內次分層。

### Phase 4 - 前後端分離（1-2 週）

- 建立 React 前端骨架並完成最小流程：Login、Listing List、Listing Detail。
- 前端 API 呼叫走統一 client，型別化 DTO，錯誤統一轉換。
- 逐步由 MVC 切換，保留短期備援。
- 重構期間 `NeighborGoods.Web` 僅供參考，不作為 API 依賴；完成切換後可直接下線移除。

#### Phase 4A - React Frontend 首波交付（2026-04 啟動）

- 新增獨立專案：`NeighborGoods.Frontend`（Vite + React + TypeScript + Tailwind）。
- 核心目錄策略：
  - `src/app`：路由、版型殼層
  - `src/features/auth`：登入、token 管理、路由守衛
  - `src/features/listings`：商品列表首頁（搜尋/分頁）
  - `src/features/messaging`：對話列表、聊天室、SignalR 即時訊息
  - `src/shared`：API client、共用型別、UI 元件、環境設定
- API client 統一策略：
  - 請求攜帶 Bearer token
  - 遇到 `401` 先嘗試 `POST /api/v1/auth/refresh`
  - refresh 失敗則清空登入狀態並導回登入頁
- 路由優先順序：
  1. Login（`/login`）
  2. Listing Home（`/listings`）
  3. Messages（`/messages`、`/messages/{conversationId}`，需授權）
- 視覺規範已建立：`docs/frontend-style-guide.md`（色票、字級、元件、頁面模板）。

#### Phase 4A 驗收標準

- `NeighborGoods.Frontend` 可成功 `npm run build`。
- 登入成功後可持續登入（重整頁面不掉登入）。
- 商品首頁可完成載入、搜尋、分頁，並具備 loading/empty/error 三態。
- 訊息頁可列出對話、查詢歷史訊息、送出訊息、呼叫已讀 API，並可透過 SignalR 收到 `ReceiveMessage` 推播。
- 所有新頁面遵循 `docs/frontend-style-guide.md` token 與元件規範。

#### Phase 4A 風險與控管

- DTO 欄位命名差異：前端需保留 adapter/typed API 邊界，避免 UI 直接耦合後端實體。
- 訊息分頁與即時推播可能重複：以訊息 ID 去重後再排序。
- API 本機位址差異：統一由 `.env` (`VITE_API_BASE_URL`, `VITE_SIGNALR_BASE_URL`) 管理。

#### Phase 4B - Frontend 設計二階優化（規格定稿）

- Hero 副標文案統一為「社宅專屬二手交易平台」。
- 首頁進場動畫採中等節奏（450ms 淡入 + 上升 20px），並定義區塊延遲序列（品牌、主標、副標、篩選區）。
- 篩選 UX 由表單式改為按鈕式即時查詢：
  - 展開型多選：分類 / 品況 / 社宅
  - 直接切換：是否免費 / 愛心捐贈 / 以物易物
  - 保留清除條件，一鍵清空並重查
- 快速條件按鈕採柔和語意色（低飽和）：
  - 免費（綠）
  - 愛心捐贈（粉橘）
  - 以物易物（藍紫）
- 動效統一：按鈕 hover/active、展開過場、手機 bottom sheet 轉場與列表重查 skeleton 節奏。
- 響應式規格定稿：
  - Desktop 同列呈現展開型與快速條件
  - Tablet 上下兩列
  - Mobile 兩排按鈕 + bottom sheet 展開

#### Phase 4B 驗收標準（設計層）

- 文件已完整定義文案、按鈕型態、query 行為、語意色 token、動畫參數與 RWD 斷點行為。
- 設計規格已回寫至 `docs/frontend-style-guide.md`，可直接作為後續實作依據。

### Phase 5 - 安全與上線收斂（3-5 天，部分已完成）

- JWT + Refresh Token（目前採 JSON body 回傳）已導入。
- CORS 白名單、rate limiting、全域錯誤處理、關鍵審計日誌。
- CI/CD 補強：`dotnet test`（Testcontainers）、部署後 health check、回滾 SOP。
- 待補：refresh token storage hardening（可升級專用表與裝置綁定策略）、token 旋轉風險監控。

## 5. API 契約基準（v1）

- Base path：`/api/v1`
- 回應格式：
  - 成功：`{ success: true, data, meta }`
  - 失敗：`{ success: false, error, meta }`
- 分頁欄位：`page`、`pageSize`、`totalCount`、`totalPages`
- Listing 契約（目前已實作）：
  - `GET /api/v1/listings?q=&page=1&pageSize=20`
  - `GET /api/v1/listings/{id}`
  - `POST /api/v1/listings`
  - `PUT /api/v1/listings/{id}`
  - `DELETE /api/v1/listings/{id}`
- Lookups 契約（目前已實作）：
  - `GET /api/v1/lookups/categories`
  - `GET /api/v1/lookups/conditions`
  - `GET /api/v1/lookups/residences`
  - `GET /api/v1/lookups/pickup-locations`
- Auth 契約（目前已實作）：
  - `POST /api/v1/auth/login`
  - `POST /api/v1/auth/refresh`
  - `POST /api/v1/auth/revoke`
  - `GET /api/v1/auth/line/login`
  - `GET /api/v1/auth/line/callback`
- Account 契約（目前已實作）：
  - `POST /api/v1/account/register/send-code`
  - `POST /api/v1/account/register`
  - `POST /api/v1/account/email/send-code`
  - `POST /api/v1/account/email/verify`
  - `GET /api/v1/account/me`
  - `PATCH /api/v1/account/me`
  - `POST /api/v1/account/line/bind/start`
  - `GET /api/v1/account/line/bind/status?pendingBindingId=`
  - `POST /api/v1/account/line/bind/confirm`
  - `POST /api/v1/account/line/bind/unbind`
  - `POST /api/v1/integrations/line/webhook`（Webhook 驗簽與 follow/unfollow 事件）
- 對話／訊息（REST + SignalR；不實作站內購買／同意／完成交易流程；建立對話須包含該商品賣家）：
  - `POST /api/v1/conversations`（body：`listingId`, `otherUserId`；僅建立或取得對話，不發訊）
  - `GET /api/v1/conversations`
  - `GET /api/v1/conversations/{id}/messages?page=&pageSize=`
  - `POST /api/v1/conversations/{id}/messages`（body：`content`）
  - `POST /api/v1/conversations/{id}/read`
  - SignalR Hub：`/hubs/messages`（連線時 query 帶 `access_token`；發訊成功後伺服器對雙方推播 `ReceiveMessage`）
- 主要狀態碼：
  - `200` 成功查詢/更新/刪除
  - `201` 建立成功
  - `400` 請求驗證失敗
  - `401` 未登入或 token 無效
  - `403` 已登入但權限或驗證條件不足（例如 Email 未驗證）
  - `404` 資源不存在（`LISTING_NOT_FOUND`）

## 6. 設定鍵最小清單（必填優先）

### 必填

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`
- `Jwt__AccessTokenMinutes`
- `Jwt__RefreshTokenDays`
- `Line__ChannelId`
- `Line__ChannelSecret`
- `Line__CallbackUrl`
- `AzureBlob__ConnectionString`

### 建議

- `AzureBlob__ContainerName`
- `LineMessagingApi__ChannelAccessToken`
- `LineMessagingApi__ChannelSecret`
- `EmailNotification__ConnectionString`
- `EmailNotification__FromEmailAddress`

## 7. 部署與驗收清單（每次上線）

### 上線前

- `dotnet restore`
- `dotnet build -c Release`
- 有 migration 時確認可套用與可回滾
- 本地 smoke test 完成

### 上線中

- 檢查 GitHub Actions build/publish/deploy 全綠
- 確認 Azure 設定鍵完整且值正確

### 上線後

- 首頁/API 可開啟且無 500
- 商品清單/詳情可讀
- 刊登與圖片上傳可用
- 訊息流程正常
- 監控無持續錯誤

### 回滾

- 保留上一版 artifact 或 commit SHA
- 記錄本次 migration 名稱
- 發生重大異常先回滾應用，再處理資料層

## 8. 本週執行清單（建議）

1. 將 JWT/LINE secrets 從開發設定搬到環境變數/KeyVault（清理硬編碼風險）。
2. 規劃 Refresh Token 強化（裝置識別、撤銷追蹤、異常行為告警）。
3. Lookup table：`Category` / `Condition` / `Residence` / `PickupLocation` 與對應 API 已完成。
4. `GET /api/v1/lookups/conditions`、`residences`、`pickup-locations` 已完成。
5. 加入部署後 health check 與告警門檻。

## 9. 文件管理規則（從現在開始）

- `docs` 只保留本檔：`docs/refactor-plan.md`。
- 新需求、決策、排程、檢查表都只更新本檔。
- 若未來需要拆分文件，需先確認此檔會保持「總索引 + 單一真實來源」角色。
