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
  - If `LiffUrl` is provided (e.g. `https://liff.line.me/{LiffId}`), all four URI areas use `?liff.state=/...` deep links
  - Otherwise fallback to `$WebBaseUrl` + path
- Row 1, Area 2: postback `action=myListings`
- Row 1, Area 3: postback `action=myMessages`
- Row 2, Area 1: open `$WebBaseUrl/listings/create` (`uri`)
- Row 2, Area 2: open `$WebBaseUrl/account` (`uri`, or LIFF `liff.state=/account`)
- Row 2, Area 3: open `$WebBaseUrl/favorites` (`uri`)

Postback values are aligned with current backend webhook routing.

Current webhook reply behavior:

- `action=myListings`
  - Reply a Flex carousel first (max 5 listing cards)
  - Each card includes listing status, favorite count, and a LIFF deep link to `/listings/{id}` when `LineMessagingApi:LiffId` is set
  - Ordering priority: has unread messages > recently changed status (proxied by latest update time) > latest updated/created
- `action=myMessages`
  - Reply unread summary first
  - Include up to 3 quick links to unread conversations (`/messages/{conversationId}`)
  - Include a fallback button to `/messages`

## Example usage

```powershell
pwsh "./infra/line/line-richmenu.ps1" `
  -ChannelAccessToken "..." `
  -RichMenuImagePath "C:\github\NGDSH_NeighborGoods\infra\line\linemenu.png" `
  -WebBaseUrl "https://www.neighborgoodstw.com/" `
  -LiffUrl "https://liff.line.me/2008745853-Ui8PkOGi" `
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

- **LIFF**：在 **LINE Login channel**（與網站 LINE 登入同一個）建立 LIFF，Endpoint URL 為 `{WebBaseUrl}/`（網站根目錄；須 HTTPS；本機可用 tunnel）。同一 LIFF 兼用深層連結與綁定流程。
- **深層連結**：LINE 內分享常產生 `?liff.state=/listings/{id}` 等形式；前端根路由 `RootEntry` 會讀取 `liff.state` 並導向對應頁面。請勿依賴 path 格式 `liff.line.me/{liffId}/listings/{id}`（實測 uuid 段可能被 LINE 截斷）。
- **綁定 URL**：後端產生 `https://liff.line.me/{LiffId}?liff.state=/liff/line-notify&bindToken=...&botLink=...`（`liff.state` 只帶 path，`bindToken`/`botLink` 放在外層 query；勿嵌套在 `liff.state` 內，LINE 會丟失）。
- **後端**：Container App 環境變數 `LineMessagingApi__WebBaseUrl`、`LineMessagingApi__LiffId`（與前端 `VITE_LINE_LIFF_ID` 一致）；`id_token` 驗證使用 `Line__ChannelId`（Login channel）。
- **Console**：Login channel 與官方帳號 **Link a bot**；LIFF **Add friend option**（建議 On aggressive）由 LINE 引導加好友。綁定頁只以 `id_token` 寫入 DB，不再以 `getFriendship()` 阻擋綁定。

舊 MVC 專案（`NeighborGoods.Web`）已自本 repo 移除；正式環境以 SPA + API + LIFF 綁定為準。
