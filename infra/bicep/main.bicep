targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Environment name (e.g. prod)')
param environmentName string = 'prod'

@description('Global name prefix, only lowercase letters and numbers are recommended')
param namePrefix string = 'neighborgoods'

@description('App Service Plan SKU name. Use F1 for lowest cost (Windows).')
param appServicePlanSkuName string = 'F1'

@description('App Service Plan SKU tier')
param appServicePlanSkuTier string = 'Free'

@description('Deploy Azure SignalR Service or not')
param deploySignalR bool = true

@description('Azure SQL admin login')
param sqlAdminLogin string

@secure()
@description('Azure SQL admin password')
param sqlAdminPassword string

@secure()
@description('LINE OIDC ChannelId')
param lineOidcChannelId string

@secure()
@description('LINE OIDC ChannelSecret')
param lineOidcChannelSecret string

@description('LINE Messaging API ChannelId (optional)')
param lineMessagingChannelId string = ''

@secure()
@description('LINE Messaging API ChannelAccessToken (optional)')
param lineMessagingAccessToken string = ''

@secure()
@description('LINE Messaging API ChannelSecret (optional)')
param lineMessagingChannelSecret string = ''

@description('LINE Messaging API BotId (optional), e.g. @559fslxw')
param lineMessagingBotId string = ''

@description('LINE Messaging API BaseUrl (optional)')
param lineMessagingBaseUrl string = ''

@secure()
@description('Azure Communication Services Email connection string (optional)')
param emailConnectionString string = ''

@description('Email sender address (optional)')
param emailFromAddress string = ''

@description('Email sender display name (optional)')
param emailFromDisplayName string = 'NeighborGoods'

@description('Email logo URL (optional)')
param emailLogoUrl string = ''

@description('Email base URL (optional)')
param emailBaseUrl string = ''

@description('LINE OIDC callback path')
param lineOidcCallbackPath string = '/signin-line'

@description('LINE OIDC scope')
param lineOidcScope string = 'openid profile'

@description('App environment')
param aspnetcoreEnvironment string = 'Production'

var suffix = toLower(uniqueString(resourceGroup().id, namePrefix, environmentName))
var webAppName = '${namePrefix}-${environmentName}-web-${take(suffix, 6)}'
var appServicePlanName = '${namePrefix}-${environmentName}-plan'
var storageAccountName = toLower(take(replace('${namePrefix}${environmentName}${suffix}', '-', ''), 24))
var blobContainerName = 'neighborgoods-images'
var sqlServerName = '${namePrefix}-${environmentName}-sql-${take(suffix, 6)}'
var sqlDatabaseName = '${namePrefix}-${environmentName}-db'
var signalRName = '${namePrefix}-${environmentName}-signalr-${take(suffix, 6)}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: true
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: blobContainerName
  parent: blobService
  properties: {
    publicAccess: 'Blob'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  name: sqlDatabaseName
  parent: sqlServer
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
  }
}

resource sqlFirewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  name: 'AllowAzureServices'
  parent: sqlServer
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServicePlanSkuName
    tier: appServicePlanSkuTier
  }
  kind: 'app'
  properties: {
    reserved: false
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      alwaysOn: false
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: aspnetcoreEnvironment
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: 'Server=tcp:${sqlServer.name}.database.windows.net,1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
        {
          name: 'AzureBlob__ConnectionString'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'AzureBlob__ContainerName'
          value: blobContainer.name
        }
        {
          name: 'Authentication__Line__ChannelId'
          value: lineOidcChannelId
        }
        {
          name: 'Authentication__Line__ChannelSecret'
          value: lineOidcChannelSecret
        }
        {
          name: 'Authentication__Line__CallbackPath'
          value: lineOidcCallbackPath
        }
        {
          name: 'Authentication__Line__Scope'
          value: lineOidcScope
        }
        {
          name: 'LineMessagingApi__ChannelId'
          value: lineMessagingChannelId
        }
        {
          name: 'LineMessagingApi__ChannelAccessToken'
          value: lineMessagingAccessToken
        }
        {
          name: 'LineMessagingApi__ChannelSecret'
          value: lineMessagingChannelSecret
        }
        {
          name: 'LineMessagingApi__BotId'
          value: lineMessagingBotId
        }
        {
          name: 'LineMessagingApi__BaseUrl'
          value: lineMessagingBaseUrl
        }
        {
          name: 'EmailNotification__ConnectionString'
          value: emailConnectionString
        }
        {
          name: 'EmailNotification__FromEmailAddress'
          value: emailFromAddress
        }
        {
          name: 'EmailNotification__FromDisplayName'
          value: emailFromDisplayName
        }
        {
          name: 'EmailNotification__LogoUrl'
          value: emailLogoUrl
        }
        {
          name: 'EmailNotification__BaseUrl'
          value: emailBaseUrl
        }
      ]
    }
  }
}

resource signalR 'Microsoft.SignalRService/signalR@2024-03-01-preview' = if (deploySignalR) {
  name: signalRName
  location: location
  sku: {
    name: 'Free_F1'
    tier: 'Free'
    capacity: 1
  }
  kind: 'SignalR'
  properties: {
    publicNetworkAccess: 'Enabled'
  }
}

output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output appServicePlanName string = appServicePlan.name
output storageAccountName string = storageAccount.name
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output signalRName string = deploySignalR ? signalR.name : 'not-deployed'
