param(
  [Parameter(Mandatory = $true)]
  [string]$ChannelAccessToken,

  [Parameter(Mandatory = $true)]
  [string]$RichMenuImagePath,

  [Parameter(Mandatory = $true)]
  [string]$WebBaseUrl,

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

$normalizedBaseUrl = $WebBaseUrl.TrimEnd("/")
$apiBase = "https://api.line.me/v2/bot"

function New-AuthHeadersJson {
  param([string]$Token)
  return @{
    Authorization = "Bearer $Token"
    "Content-Type" = "application/json"
  }
}

function New-AuthHeadersBinary {
  param(
    [string]$Token,
    [string]$ContentType
  )
  return @{
    Authorization = "Bearer $Token"
    "Content-Type" = $ContentType
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

function Build-RichMenuDefinition {
  param(
    [string]$Name,
    [string]$BarText,
    [string]$BaseUrl
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
          uri = "$BaseUrl/"
        }
      },
      @{
        bounds = @{ x = 833; y = 0; width = 834; height = 843 }
        action = @{
          type = "postback"
          data = "action=myListings"
          displayText = "My listings"
        }
      },
      @{
        bounds = @{ x = 1667; y = 0; width = 833; height = 843 }
        action = @{
          type = "postback"
          data = "action=myMessages"
          displayText = "My messages"
        }
      },
      # Row 2
      @{
        bounds = @{ x = 0; y = 843; width = 833; height = 843 }
        action = @{
          type = "uri"
          uri = "$BaseUrl/listings/create"
        }
      },
      @{
        bounds = @{ x = 833; y = 843; width = 834; height = 843 }
        action = @{
          type = "uri"
          uri = "$BaseUrl/profile"
        }
      },
      @{
        bounds = @{ x = 1667; y = 843; width = 833; height = 843 }
        action = @{
          type = "uri"
          uri = "$BaseUrl/favorites"
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
$definition = Build-RichMenuDefinition `
  -Name $RichMenuName `
  -BarText $ChatBarText `
  -BaseUrl $normalizedBaseUrl
$createResponse = Invoke-LineApiJson -Method "POST" -Uri "$apiBase/richmenu" -Body $definition
$newRichMenuId = $createResponse.richMenuId
if ([string]::IsNullOrWhiteSpace($newRichMenuId)) {
  throw "LINE API did not return richMenuId."
}
Write-Host "Created richMenuId: $newRichMenuId"

Write-Host "[2/4] Uploading rich menu image..."
$binaryHeaders = New-AuthHeadersBinary -Token $ChannelAccessToken -ContentType $ImageContentType
Invoke-RestMethod `
  -Method "POST" `
  -Uri "$apiBase/richmenu/$newRichMenuId/content" `
  -Headers $binaryHeaders `
  -InFile $RichMenuImagePath
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
