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

- If `LiffUrl` is provided (e.g. `https://liff.line.me/{LiffId}`), URI areas use path links `liff.line.me/{LiffId}/listings` etc.
- Otherwise URI areas fallback to `$WebBaseUrl` + path
- Row 1, Area 1: open listings (`uri` `/listings`)
- Row 1, Area 2: postback `action=myListings`（Functions webhook 回 Flex carousel）
- Row 1, Area 3: postback `action=myMessages`（Functions webhook 回未讀摘要 Flex）
- Row 2, Area 1: open create listing (`uri` `/listings/create`)
- Row 2, Area 2: open account (`uri` `/account`)
- Row 2, Area 3: open favorites (`uri` `/favorites`)

Postback 由 Azure Functions `POST /api/line/webhook` 處理（需在 LINE Developers 將 Webhook URL 指到 Function App）。

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
  2. Verify image map, URI links, and postback Flex replies
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
   - Smoke-test URI menu areas and postback Flex replies
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

「我的帳號」綁定官方通知改為在 LINE 內開 LIFF 完成。

- **LIFF**：在 **LINE Login channel**（與網站 LINE 登入同一個）建立 LIFF，Endpoint URL 為 `{WebBaseUrl}/`（網站根目錄；須 HTTPS；本機可用 tunnel）。同一 LIFF 兼用深層連結與綁定流程。
- **深層連結**：圖文選單、官方 Flex 按鈕（含未綁定提示「前往個人設定」）使用 path 格式 `liff.line.me/{liffId}/account` 等；`RootEntry` 仍支援舊 `?liff.state=`。商品 Flex「查看商品」：`liff.line.me/{liffId}/listings/{id}?from=listings`。
- **綁定 URL**：`https://liff.line.me/{LiffId}/?bindToken=...&botLink=...`（根路徑 + 外層 query，在 `/` 完成 liff.init）。
- **後端**：Container App 環境變數 `LineMessagingApi__WebBaseUrl`、`LineMessagingApi__LiffId`（與前端 `VITE_LINE_LIFF_ID` 一致）；`id_token` 驗證使用 `Line__ChannelId`（Login channel）。
- **Console**：Login channel 與官方帳號 **Link a bot**；LIFF **Add friend option**（建議 On aggressive）由 LINE 引導加好友。綁定頁只以 `id_token` 寫入 DB，不再以 `getFriendship()` 阻擋綁定。

舊 MVC 專案（`NeighborGoods.Web`）已自本 repo 移除；正式環境以 SPA + API + LIFF 綁定為準。

## 交易群組（自動取得 groupId）

刊登成功後可「分享到交易群組」，由官方帳號把商品 Flex 卡推到全站唯一的 LINE **一般群組**（不是社群）。

### LINE Developers 設定

1. Messaging API channel 的 Webhook URL 設為：
   - API：`https://{api-host}/api/v1/integrations/line/webhook`
   - 或既有 Azure Functions：`https://{functions-host}/api/line/webhook`
2. 開啟 **Use webhook**。
3. 官方帳號允許加入群組（Allow bot to join group chats）。
4. 確認 `LineMessagingApi__ChannelSecret` 已設定（簽章驗證用）。

### 如何觸發並自動存 groupId

程式無法主動向 LINE 查詢群組清單。下列任一動作會讓 webhook 自動寫入資料庫：

1. **把官方帳號加入交易群組**（主路徑，`join` 事件）。
2. **官方帳號已在群裡**：請群內有人 @ 官方帳號一次（`message` 事件備援）。

第一個收到的群會自動設為交易群；之後從其他群收到的不同 `groupId` 只記錄、不覆蓋。

### 後台確認

開啟後台首頁「LINE 推播診斷」→「交易群組」：

- 尚未綁定：會顯示觸發說明。
- 已綁定：顯示完整 `groupId`、加入時間、複製按鈕。
- 其他已記錄群組可按「設為交易群」手動切換。
