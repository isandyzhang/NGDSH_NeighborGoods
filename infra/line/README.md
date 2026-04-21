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
  -WebBaseUrl "https://your-frontend.example.com" `
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
