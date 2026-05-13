# LINE Rich Menu Script

This folder contains a PowerShell script that creates and deploys one LINE rich menu using Messaging API.

## Files

- `line-richmenu.ps1`: Create rich menu, upload image, assign to all users.

## Prerequisites

- A valid LINE Messaging API channel access token
- PowerShell 7+ (recommended)
- A rich menu image in supported size/format
  - Script uses full-size 6-area layout: `2500x1686` (2 rows x 3 columns)
  - Content type must match file:
    - PNG: `image/png`
    - JPG/JPEG: `image/jpeg`

## Actions configured in this script

- Row 1, Area 1: open listings entry (`uri`)
  - If `LiffUrl` is provided, use LIFF URL (recommended for in-LINE experience)
  - Otherwise fallback to `$WebBaseUrl/listings`
- Row 1, Area 2: postback `action=myListings`
- Row 1, Area 3: postback `action=myMessages`
- Row 2, Area 1: open `$WebBaseUrl/listings/create` (`uri`)
- Row 2, Area 2: open `$WebBaseUrl/profile` (`uri`)
- Row 2, Area 3: open `$WebBaseUrl/favorites` (`uri`)

Postback values are aligned with current backend webhook routing.

Current webhook reply behavior:

- `action=myListings`
  - Reply a Flex carousel first (max 5 listing cards)
  - Each card includes listing status, favorite count, and a deep link to `/listings/{id}`
  - Ordering priority: has unread messages > recently changed status (proxied by latest update time) > latest updated/created
- `action=myMessages`
  - Reply unread summary first
  - Include up to 3 quick links to unread conversations (`/messages/{conversationId}`)
  - Include a fallback button to `/messages`

## Example usage

```powershell
pwsh "./infra/line/line-richmenu.ps1" `
  -ChannelAccessToken "Yza5/xt9annQc5UGZOX0phmWJLO3Ic4T+Ieho9BREqvUvzbAD48MZRjxHx/ED8rRRpe6IZHqcqqcJIIEqC9EHcHLP+sMKWY+K8l0fe9ukD8oiCkJYUCh6r1fmYcO9S7WiF+OCsskHElU95NKtHaGPAdB04t89/1O/w1cDnyilFU=" `
  -RichMenuImagePath "C:\github\NGDSH_NeighborGoods\infra\line\linemenu.png" `
  -WebBaseUrl "https://www.neighborgoodstw.com/" `
  -ImageContentType "image/png"
```

## Optional flags

- `-AssignToAllUsers:$false`
  - Create and upload only, do not set as default rich menu.
- `-DeleteOldDefaultRichMenu:$true`
  - After assigning new rich menu, delete previous default rich menu.

## Notes

- The script creates a new rich menu each run.
- Recommended release flow:
  1. Run in test/staging OA account first
  2. Verify image map and postback behavior
  3. Run in production OA account

## Deployment strategy (IaC + CI/CD)

For production maintenance, prefer version-controlled deployment instead of manual-only runs.

1. Keep rich menu config and assets in repo
   - Script: `infra/line/line-richmenu.ps1`
   - Rich menu image: keep versioned file (for rollback and audit)
2. Configure environment-specific secrets in CI/CD
   - `LINE_CHANNEL_ACCESS_TOKEN`
   - `LineMessagingApi__ChannelSecret`
   - `LineMessagingApi__WebBaseUrl`
   - `LineMessagingApi__LiffId`
   - `Line__ChannelId`
3. In pipeline, run rich menu deployment after backend/frontend deploy
   - Deploy to staging first
   - Smoke-test postback actions
   - Deploy to production
4. Rollback strategy
   - Keep previous richMenuId in deployment output/log
   - Re-assign previous richMenuId if newly deployed menu has issues

## GitHub Actions workflow

Workflow file:

- `.github/workflows/line_richmenu_cd.yml`

How to run:

1. Go to GitHub Actions and choose `LINE Rich Menu CD`
2. Click `Run workflow`
3. The workflow is fixed to production defaults:
   - Environment: `production`
   - Assign to all users: `true`
   - Delete old default rich menu: `true`
   - Image path: `infra/line/menu.jpg`
   - Image content type: `image/jpeg`

Required GitHub Actions secrets (set in `production` Environment):

- `LINE_CHANNEL_ACCESS_TOKEN`
- `LINE_WEB_BASE_URL`
- `LINE_LIFF_URL` (optional but recommended, e.g. `https://liff.line.me/2008745853-Ui8PkOGi`)

Optional GitHub Actions variables (set in `production` Environment):

- `LINE_RICHMENU_NAME` (default: `NeighborGoods Main Menu`)
- `LINE_CHAT_BAR_TEXT` (default: `Open menu`)

## LINE 官方通知綁定（LIFF）

「我的帳號」綁定官方通知改為在 LINE 內開 LIFF 完成，不再依 webhook follow 自動寫入 pending。

- **LIFF**：在 **LINE Login channel**（與網站 LINE 登入同一個）建立 LIFF，Endpoint URL 建議設為 `{WebBaseUrl}` 根目錄（須 HTTPS；本機可用 tunnel），實際功能頁以 LIFF deep link path 指向（例如 `/liff/line-notify`）。
- **後端**：Container App 環境變數 `LineMessagingApi__WebBaseUrl`、`LineMessagingApi__LiffId`（與前端 `VITE_LINE_LIFF_ID` 一致）；`id_token` 驗證使用 `Line__ChannelId`（Login channel）。
- **Console**：Login channel 與官方帳號 **Link a bot**，LIFF 可加 **Add friend** 以利 `getFriendship()` 與推播。
- **擴充建議**：採用單一 LIFF App + 多路徑（`/liff/...`）模式，可依功能擴充不同頁面，不需每個功能額外建立新 LIFF App。

`NeighborGoods.Web`（舊 MVC）仍含依 follow 寫入 pending 的綁定流程；若正式環境已僅使用 SPA + API，該路徑可視為遺留，之後再移除或改為導向新站即可。

## LINE 登入 state 為何不再隨容器重啟失效

LINE 登入走 OAuth Authorization Code Flow，後端在 `/api/v1/auth/line/login` 會用 ASP.NET Core Data Protection 簽出一張 `state` 給 LINE，回程在 `/api/v1/auth/line/callback` 用同一把 key 解開驗證。簽 / 驗用的「key ring」預設寫在容器本機 `~/.aspnet/DataProtection-Keys/`，容器重啟或被換到別的 replica 後就會解不開上一刻發出去的 state，前端會收到 `INVALID_LINE_STATE`。

為了讓 key ring 跨重啟與跨 replica 都活著，做法是：

- Bicep 在既有 Storage Account 多開一個私有容器 `dp-keys`（`publicAccess: 'None'`），同時定義在 `infra/bicep/modules/legacy/storage.bicep` 與 `infra/bicep/modules/storage-manage-existing.bicep`，新建 / 沿用既有 storage 兩條路徑都覆蓋到。
- `NeighborGoods.Api/Program.cs` 在偵測到環境變數 `AzureBlob__ConnectionString` 時，會把 key ring 寫到 `dp-keys/keys.xml`；沒設則 fallback 回本機檔，本機開發不受影響。
- 不需要 Managed Identity / Key Vault；沿用既有 `AzureBlob__ConnectionString`（已注入容器 secret）即可存取。
- Blob 只在 API 啟動時讀一次載入記憶體，後續驗 state 不會額外打 Blob，登入 latency 不受影響。
