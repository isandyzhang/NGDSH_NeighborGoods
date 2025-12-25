## NeighborGoods 開發紀錄（2025-12-22）

### 一、目前整體架構概況

- **前端 / 後端**
  - C# ASP.NET Core MVC (.NET 8)，專案：`NeighborGoods.Web`
  - Razor Views + Bootstrap，採 Server-side Render

- **身分驗證與登入**
  - 使用 **ASP.NET Core Identity** + Entity Framework Core
  - 使用者實體 `ApplicationUser`：
    - `DisplayName`, `LineUserId`, `Role`（`UserRole` enum）, `CreatedAt`（台灣時間）
  - 登入方式：
    - 帳號密碼（Identity 預設流程，自訂中文錯誤訊息）
    - **LINE Login (OIDC)**：
      - 以 `AddOpenIdConnect("LINE", ...)` 串 LINE OIDC
      - 從 LINE 回傳的 `sub` + profile 建立/綁定本地帳號
      - 使用 Channel Secret 作為 HS256 簽章驗證金鑰
  - 錯誤訊息本地化：
    - `ChineseIdentityErrorDescriber` 將常見 Identity 錯誤轉成繁體中文

- **資料庫**
  - Azure SQL Database（使用 ADO.NET SQL 驗證）
  - DbContext：`AppDbContext`
    - `DbSet<ApplicationUser>`（來自 `IdentityDbContext`）
    - `DbSet<Listing> Listings`
    - `DbSet<ListingImage> ListingImages`
  - 所有時間欄位皆以 **台灣時間 (UTC+8)** 儲存：
    - 透過 `TaiwanTime.Now` 統一轉換

- **金額與時間**
  - 金額欄位 `Listing.Price`：
    - SQL 型別為 `decimal(18,0)`（台幣整數，不含小數）
  - 建立 / 更新時間：
    - `ApplicationUser.CreatedAt`
    - `Listing.CreatedAt`, `Listing.UpdatedAt`
    - `ListingImage.CreatedAt`

---

### 二、已實作功能（到 2025-12-22 為止）

#### 1. 專案初始化與 Identity
- 建立 `.NET 8` MVC 專案與 Solution
- 加入 EF Core + Identity 套件（8.0.10）
- 設定 `AppDbContext` 使用 Azure SQL 的 `DefaultConnection`
- Identity 設定：
  - 密碼規則（長度 / 數字 / 大小寫 / 特殊符號）
  - 啟用 Cookie Authentication（由 `AddIdentity` 內部設定）

#### 2. 帳號註冊 / 登入 / 登出
- `AccountController`：
  - `Register`（GET/POST）
  - `Login`（GET/POST）
  - `Logout`（POST）
- ViewModels：
  - `RegisterViewModel`（帳號、顯示名稱、密碼、確認密碼）
  - `LoginViewModel`（帳號、密碼、記住我）
- Razor Views：
  - `Views/Account/Register.cshtml`
  - `Views/Account/Login.cshtml`
  - 顯示密碼規則說明與中文錯誤訊息

#### 3. LINE Login 串接
- 設定檔：
  - `appsettings.Development.json` 中的 `Authentication:Line` 區段：
    - `CallbackPath = /signin-line`
    - `Scope = "openid profile"`
  - 機密資料（`ChannelId`, `ChannelSecret`）使用 `dotnet user-secrets` 儲存：
    - `Authentication:Line:ChannelId`
    - `Authentication:Line:ChannelSecret`
- `Program.cs`：
  - `AddOpenIdConnect("LINE", ...)` 指向 LINE 的 metadata endpoint
  - Token 驗證：
    - 使用 `TokenValidationParameters`：
      - `ValidIssuer = "https://access.line.me"`
      - `ValidAudience = ChannelId`
      - `IssuerSigningKey = SymmetricSecurityKey(ChannelSecret)`（HS256）
      - `NameClaimType = "name"`
- `AccountController`：
  - `ExternalLogin`：導向 LINE 授權頁
  - `ExternalLoginCallback`：
    - 成功授權後，先嘗試 `ExternalLoginSignInAsync`
    - 若沒有對應帳號：
      - 用 `line_{sub}` 當 `UserName`
      - 將 LINE 的 `sub` 存入 `LineUserId`
      - 將暱稱存入 `DisplayName`
      - 建立 `ApplicationUser`，再透過 `AddLoginAsync` 綁定

#### 4. 頁首導覽列與登入狀態
- `_Layout.cshtml`：
  - 透過 `UserManager<ApplicationUser>` 取得目前登入使用者
  - 若登入：
    - 顯示「哈囉，{DisplayName}」
    - 顯示「刊登商品」連結（指向 `/Listing/Create`）
    - 顯示「登出」按鈕（POST `/Account/Logout`）
  - 未登入：
    - 顯示「登入」「註冊」連結

#### 5. 商品資料結構與資料表
- 實體 `Listing`：
  - `Id (Guid)`
  - `Title`、`Description`
  - `Price (decimal, decimal(18,0))`
  - `IsFree`（免費索取）
  - `IsCharity`（愛心商品）
  - `Status`（`ListingStatus`：Active / Reserved / Sold / Donated / Inactive）
  - `SellerId (string)` + `Seller (ApplicationUser)`
  - `CreatedAt`、`UpdatedAt`（台灣時間）
  - `ICollection<ListingImage> Images`
- 實體 `ListingImage`：
  - `Id (Guid)`
  - `ListingId`（FK → `Listing`，Cascade Delete）
  - `ImageUrl`（圖片在 Azure Blob 的 URL/路徑，現階段由使用者手動填）
  - `SortOrder`（0–4，一個商品最多 5 張圖）
  - `CreatedAt`（台灣時間）
- Migrations：
  - `InitialIdentity`（Identity 相關）
  - `AddListings`（建立 `Listings` 資料表）
  - `ChangeListingPriceToInteger`（將 Price 改為 decimal(18,0)）
  - `AddListingImages`（建立 `ListingImages` 與 FK 關聯）

#### 6. 刊登商品（Create）
- ViewModel：`ListingCreateViewModel`
  - `Title`, `Description`, `Price`, `IsFree`, `IsCharity`
  - `ImageUrl1` ~ `ImageUrl5`（最多 5 張圖片 URL）
- Controller：`ListingController`（需登入）
  - `GET /Listing/Create`：
    - 回傳空的 `ListingCreateViewModel`
  - `POST /Listing/Create`：
    - 檢查 ModelState，取得目前登入使用者
    - 處理免費邏輯：若 `IsFree == true`，`Price` 強制設為 0
    - 建立 `Listing` 實體並儲存
    - 針對 `ImageUrl1~5` 非空者，建立對應 `ListingImage`：
      - `ListingId` 指向新建的商品
      - `ImageUrl` 為修剪後的 URL
      - `SortOrder` 為 0~4
    - 儲存完成後暫時導回首頁（未實作商品詳情頁）
- View：`Views/Listing/Create.cshtml`
  - Bootstrap 表單：
    - 標題、描述、價格、免費勾選、愛心勾選
    - 5 個圖片 URL 輸入框
    - 使用 `_ValidationScriptsPartial` 啟用前端驗證

---

### 三、未來待辦事項與建議路線

#### A. 商品前台展示與管理
- **首頁商品列表**
  - 建立 `Home/Index` 的商品卡片列表
  - 顯示：圖片縮圖（第一張）、標題、價格／免費、愛心標註、狀態
  - 支援分頁與簡易排序（最新刊登在前）
- **商品詳情頁**
  - 建立 `ListingController.Details(Guid id)` + `Views/Listing/Details.cshtml`
  - 顯示所有圖片（最多 5 張、輪播或縮圖列）
  - 顯示賣家暱稱、刊登時間、描述、愛心標註等
- **我的商品**
  - `Listing/My`：顯示目前登入使用者的刊登清單
  - 支援編輯商品、變更狀態（上架 / 保留 / 完成交易 / 下架）

#### B. 圖片上傳整合 Azure Blob Storage
- 建立 `BlobService`（包裝 Azure.Storage.Blobs SDK）：
  - `UploadListingImageAsync(listingId, stream, contentType)` → 回傳 URL
  - `DeleteListingImageAsync(url)` 等
- 刊登/編輯頁面：
  - 將目前的 URL 輸入改為 `<input type="file">`（多檔上傳，最多 5 個）
  - 上傳成功後取得 Blob URL，寫入 `ListingImages` 而非手動輸入 URL
- 設定：
  - `appsettings.*.json` 新增 `AzureBlob:ConnectionString`、`AzureBlob:ContainerName`
  - 開發環境可先用 Azurite 或開發用 Storage Account

#### C. 更完整的登入體驗與個人帳號頁面
- 新增 `Account/Profile`：
  - 顯示/編輯 DisplayName、Email
  - 顯示已綁定的登入方式（本地密碼 / LINE）
- 支援從 Profile 手動「綁定 / 解除綁定 LINE」
  - 供已用帳號密碼註冊的使用者之後再綁定 LINE 使用

#### D. 部署到 Azure App Service
- 建立 App Service（.NET 8）
  - 設定 ConnectionStrings、LINE Channel 設定、Blob 設定於「應用程式設定」
  - 啟用 HTTPS 強制導向
- 部署方式建議：
  - GitHub Actions（CI/CD）
  - 或 Visual Studio / `dotnet publish` + zip deploy
- 調整 LINE Developers：
  - Callback URL 增加正式站位址，例如：
    - `https://<your-app>.azurewebsites.net/signin-line`

---

### 四、短期建議優先順序（下一步）

1. **首頁商品列表 + 詳情頁**
   - 讓目前的「刊登商品」可以在前台被看到與瀏覽
2. **真正的圖片上傳（掛上 Azure Blob）**
   - 取代目前手動貼 URL 的方式
3. **我的商品 / 狀態管理**
   - 讓使用者可以管理自己的刊登（上架、保留、完成交易、下架）


