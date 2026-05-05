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

- Row 1, Area 1: open website home (`uri`)
- Row 1, Area 2: postback `action=myListings`
- Row 1, Area 3: postback `action=myMessages`
- Row 2, Area 1: open `$WebBaseUrl/listings/create` (`uri`)
- Row 2, Area 2: open `$WebBaseUrl/profile` (`uri`)
- Row 2, Area 3: open `$WebBaseUrl/favorites` (`uri`)

Postback values are aligned with current backend webhook routing.

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

## LINE 官方通知綁定（LIFF）

「我的帳號」綁定官方通知改為在 LINE 內開 LIFF 完成，不再依 webhook follow 自動寫入 pending。

- **LIFF**：在 **LINE Login channel**（與網站 LINE 登入同一個）建立 LIFF，Endpoint URL 為 `{WebBaseUrl}/liff/line-notify`（須 HTTPS；本機可用 tunnel）。
- **後端**：Container App 環境變數 `LineMessagingApi__WebBaseUrl`、`LineMessagingApi__LiffId`（與前端 `VITE_LINE_LIFF_ID` 一致）；`id_token` 驗證使用 `Line__ChannelId`（Login channel）。
- **Console**：Login channel 與官方帳號 **Link a bot**，LIFF 可加 **Add friend** 以利 `getFriendship()` 與推播。

`NeighborGoods.Web`（舊 MVC）仍含依 follow 寫入 pending 的綁定流程；若正式環境已僅使用 SPA + API，該路徑可視為遺留，之後再移除或改為導向新站即可。
