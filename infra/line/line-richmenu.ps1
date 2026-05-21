param(
  [Parameter(Mandatory = $true)]
  [string]$ChannelAccessToken,

  [Parameter(Mandatory = $true)]
  [string]$RichMenuImagePath,

  [Parameter(Mandatory = $true)]
  [string]$WebBaseUrl,

  [string]$LiffUrl = "",

  [ValidateSet("image/png", "image/jpeg")]
  [string]$ImageContentType = "image/png",

  [string]$RichMenuName = "NeighborGoods Main Menu",
  [string]$ChatBarText = "Open menu",

  [switch]$AssignToAllUsers = $true,
  [switch]$DeleteOldDefaultRichMenu = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $RichMenuImagePath)) {
  throw "Image file not found: $RichMenuImagePath"
}

if (-not $WebBaseUrl.StartsWith("http://") -and -not $WebBaseUrl.StartsWith("https://")) {
  throw "WebBaseUrl must start with http:// or https://"
}

if (-not [string]::IsNullOrWhiteSpace($LiffUrl) -and -not $LiffUrl.StartsWith("https://liff.line.me/")) {
  throw "LiffUrl must start with https://liff.line.me/ when provided."
}

$normalizedBaseUrl = $WebBaseUrl.TrimEnd("/")
$apiBase = "https://api.line.me/v2/bot"
$apiDataBase = "https://api-data.line.me/v2/bot"

function New-AuthHeadersJson {
  param([string]$Token)
  return @{
    Authorization = "Bearer $Token"
    "Content-Type" = "application/json"
  }
}

function Invoke-LineApiJson {
  param(
    [string]$Method,
    [string]$Uri,
    [object]$Body = $null
  )

  $headers = New-AuthHeadersJson -Token $ChannelAccessToken
  if ($null -eq $Body) {
    return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers
  }

  $jsonBody = $Body | ConvertTo-Json -Depth 15
  return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers -Body $jsonBody
}

function Invoke-LineImageUpload {
  param(
    [string]$Uri,
    [string]$Token,
    [string]$ContentType,
    [string]$FilePath
  )

  $curlCommand = if (Get-Command "curl.exe" -ErrorAction SilentlyContinue) { "curl.exe" } else { "curl" }
  $curlArgs = @(
    "--fail",
    "--show-error",
    "--silent",
    "--request", "POST",
    "--url", $Uri,
    "--header", "Authorization: Bearer $Token",
    "--header", "Content-Type: $ContentType",
    "--data-binary", "@$FilePath"
  )

  $null = & $curlCommand @curlArgs
  if ($LASTEXITCODE -ne 0) {
    throw "$curlCommand upload failed with exit code $LASTEXITCODE"
  }
}

function Build-LiffDeepLink {
  param(
    [string]$LiffUrl,
    [string]$WebBaseUrl,
    [string]$InternalPath
  )

  if (-not $InternalPath.StartsWith("/")) {
    $InternalPath = "/$InternalPath"
  }

  if (-not [string]::IsNullOrWhiteSpace($LiffUrl)) {
    $liffBase = $LiffUrl.Trim().TrimEnd("/")
    $qIndex = $liffBase.IndexOf("?")
    if ($qIndex -ge 0) {
      $liffBase = $liffBase.Substring(0, $qIndex)
    }
    $encoded = [System.Uri]::EscapeDataString($InternalPath)
    return "${liffBase}?liff.state=$encoded"
  }

  return "$WebBaseUrl$InternalPath"
}

function Build-RichMenuDefinition {
  param(
    [string]$Name,
    [string]$BarText,
    [string]$ListingsUrl,
    [string]$CreateListingUrl,
    [string]$AccountUrl,
    [string]$FavoritesUrl
  )

  return @{
    size = @{
      width = 2500
      height = 1686
    }
    selected = $true
    name = $Name
    chatBarText = $BarText
    areas = @(
      # Row 1
      @{
        bounds = @{ x = 0; y = 0; width = 833; height = 843 }
        action = @{
          type = "uri"
          uri = $ListingsUrl
        }
      },
      @{
        bounds = @{ x = 833; y = 0; width = 834; height = 843 }
        action = @{
          type = "postback"
          data = "action=myListings"
          displayText = "正在查詢我的商品(｡•́ω•ˋ｡)"
        }
      },
      @{
        bounds = @{ x = 1667; y = 0; width = 833; height = 843 }
        action = @{
          type = "postback"
          data = "action=myMessages"
          displayText = "我是個愛查看訊息的好人 ψ(｀∇´)ψ ！"
        }
      },
      # Row 2
      @{
        bounds = @{ x = 0; y = 843; width = 833; height = 843 }
        action = @{
          type = "uri"
          uri = $CreateListingUrl
        }
      },
      @{
        bounds = @{ x = 833; y = 843; width = 834; height = 843 }
        action = @{
          type = "uri"
          uri = $AccountUrl
        }
      },
      @{
        bounds = @{ x = 1667; y = 843; width = 833; height = 843 }
        action = @{
          type = "uri"
          uri = $FavoritesUrl
        }
      }
    )
  }
}

function Get-CurrentDefaultRichMenuId {
  try {
    $res = Invoke-LineApiJson -Method "GET" -Uri "$apiBase/user/all/richmenu"
    return $res.richMenuId
  }
  catch {
    return $null
  }
}

Write-Host "[1/4] Creating rich menu..."
$trimmedLiffUrl = $LiffUrl.Trim()
$listingsUrl = Build-LiffDeepLink -LiffUrl $trimmedLiffUrl -WebBaseUrl $normalizedBaseUrl -InternalPath "/listings"
$createListingUrl = Build-LiffDeepLink -LiffUrl $trimmedLiffUrl -WebBaseUrl $normalizedBaseUrl -InternalPath "/listings/create"
$accountUrl = Build-LiffDeepLink -LiffUrl $trimmedLiffUrl -WebBaseUrl $normalizedBaseUrl -InternalPath "/account"
$favoritesUrl = Build-LiffDeepLink -LiffUrl $trimmedLiffUrl -WebBaseUrl $normalizedBaseUrl -InternalPath "/favorites"
if ([string]::IsNullOrWhiteSpace($trimmedLiffUrl)) {
  Write-Warning "LiffUrl not set; rich menu URI actions use WebBaseUrl only (LIFF init may fail in LINE in-app browser)."
}
else {
  Write-Host "Using LIFF deep links (liff.state) for all URI menu areas."
}
Write-Host "  listings: $listingsUrl"
$definition = Build-RichMenuDefinition `
  -Name $RichMenuName `
  -BarText $ChatBarText `
  -ListingsUrl $listingsUrl `
  -CreateListingUrl $createListingUrl `
  -AccountUrl $accountUrl `
  -FavoritesUrl $favoritesUrl
$createResponse = Invoke-LineApiJson -Method "POST" -Uri "$apiBase/richmenu" -Body $definition
$newRichMenuId = [string]$createResponse.richMenuId
$newRichMenuId = $newRichMenuId.Trim()
if ([string]::IsNullOrWhiteSpace($newRichMenuId)) {
  throw "LINE API did not return richMenuId."
}
Write-Host "Created richMenuId: $newRichMenuId"

Write-Host "[2/4] Uploading rich menu image..."
$uploadUri = "$apiDataBase/richmenu/$newRichMenuId/content"
Write-Host "Upload target: $uploadUri"
try {
  Invoke-LineImageUpload `
    -Uri $uploadUri `
    -Token $ChannelAccessToken `
    -ContentType $ImageContentType `
    -FilePath $RichMenuImagePath
}
catch {
  Write-Warning "Upload failed permanently. Cleaning up created rich menu: $newRichMenuId"
  try {
    Invoke-RestMethod `
      -Method "DELETE" `
      -Uri "$apiBase/richmenu/$newRichMenuId" `
      -Headers @{ Authorization = "Bearer $ChannelAccessToken" }
  }
  catch {
    Write-Warning "Cleanup failed for rich menu: $newRichMenuId"
  }
  throw
}
Write-Host "Image uploaded."

$oldDefaultRichMenuId = $null
if ($AssignToAllUsers) {
  Write-Host "[3/4] Assigning rich menu to all users..."
  $oldDefaultRichMenuId = Get-CurrentDefaultRichMenuId
  Invoke-RestMethod `
    -Method "POST" `
    -Uri "$apiBase/user/all/richmenu/$newRichMenuId" `
    -Headers @{ Authorization = "Bearer $ChannelAccessToken" }
  Write-Host "Assigned as default rich menu."
}
else {
  Write-Host "[3/4] Skipped default assignment (AssignToAllUsers = false)."
}

if ($DeleteOldDefaultRichMenu -and -not [string]::IsNullOrWhiteSpace($oldDefaultRichMenuId) -and $oldDefaultRichMenuId -ne $newRichMenuId) {
  Write-Host "[4/4] Deleting old default rich menu: $oldDefaultRichMenuId"
  Invoke-RestMethod `
    -Method "DELETE" `
    -Uri "$apiBase/richmenu/$oldDefaultRichMenuId" `
    -Headers @{ Authorization = "Bearer $ChannelAccessToken" }
  Write-Host "Old default rich menu deleted."
}
else {
  Write-Host "[4/4] Skip deleting old rich menu."
}

Write-Host ""
Write-Host "Done."
Write-Host "New richMenuId: $newRichMenuId"
