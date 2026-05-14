targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Resource name prefix')
param namePrefix string

@description('Environment name')
param environmentName string = 'prod'

@description('Function App Linux FX stack（例 DOTNET-ISOLATED|10.0）')
param functionsLinuxFxVersion string = 'DOTNET-ISOLATED|10.0'

@description('App environment')
param aspnetcoreEnvironment string = 'Production'

@secure()
@description('SQL connection string（ConnectionStrings:DefaultConnection）')
param sqlConnectionString string

@description('是否寫入 Azure SignalR 連線字串')
param injectSignalR bool = false

@secure()
@description('Azure SignalR connection string')
param signalRConnectionString string = ''

@secure()
@description('ACS Email connection string（可留白略過 Email 設定）')
param emailConnectionString string = ''

@description('Email 寄件人位址')
param emailFromAddress string = ''

@description('LINE Messaging ChannelId')
param lineMessagingChannelId string = ''

@secure()
@description('LINE Messaging ChannelAccessToken')
param lineMessagingChannelAccessToken string = ''

@secure()
@description('LINE Messaging ChannelSecret')
param lineMessagingChannelSecret string = ''

@description('LINE Messaging BotId')
param lineMessagingBotId string = ''

@description('LINE Messaging BaseUrl')
param lineMessagingBaseUrl string = ''

@description('前端網址（LineMessagingApi:WebBaseUrl）')
param lineMessagingWebBaseUrl string = ''

@description('LIFF App ID')
param lineMessagingLiffId string = ''

var functionAppName = '${namePrefix}-${environmentName}-func'
var hostingPlanName = '${namePrefix}-${environmentName}-funcplan'
var storageSuffix = toLower(uniqueString(resourceGroup().id, namePrefix, environmentName, 'funcjobs'))
// 儲存體名稱 3–24 字元；前置字元避免 BCP334（過短）
var funcStorageAccountName = toLower(take('ngf${replace('${namePrefix}${environmentName}${storageSuffix}', '-', '')}', 24))
var contentShareName = take(replace(toLower(functionAppName), '-', ''), 63)

var injectEmail = !empty(emailConnectionString)

resource funcStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: funcStorageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${funcStorage.name};AccountKey=${funcStorage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: true
  }
}

var coreAppSettings = [
  {
    name: 'AzureWebJobsStorage'
    value: storageConnectionString
  }
  {
    name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
    value: storageConnectionString
  }
  {
    name: 'WEBSITE_CONTENTSHARE'
    value: contentShareName
  }
  {
    name: 'FUNCTIONS_EXTENSION_VERSION'
    value: '~4'
  }
  {
    name: 'FUNCTIONS_WORKER_RUNTIME'
    value: 'dotnet-isolated'
  }
  {
    name: 'WEBSITE_ENABLE_SYNC_UPDATE_SITE'
    value: 'true'
  }
  {
    name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
    value: 'false'
  }
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: aspnetcoreEnvironment
  }
  {
    name: 'ConnectionStrings__DefaultConnection'
    value: sqlConnectionString
  }
  {
    name: 'LineMessagingApi__ChannelId'
    value: lineMessagingChannelId
  }
  {
    name: 'LineMessagingApi__ChannelAccessToken'
    value: lineMessagingChannelAccessToken
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
    name: 'LineMessagingApi__WebBaseUrl'
    value: lineMessagingWebBaseUrl
  }
  {
    name: 'LineMessagingApi__LiffId'
    value: lineMessagingLiffId
  }
]

var signalRAppSettings = injectSignalR && !empty(signalRConnectionString)
  ? [
      {
        name: 'Azure__SignalR__ConnectionString'
        value: signalRConnectionString
      }
    ]
  : []

var emailAppSettings = injectEmail
  ? [
      {
        name: 'EmailNotification__ConnectionString'
        value: emailConnectionString
      }
      {
        name: 'EmailNotification__FromEmailAddress'
        value: emailFromAddress
      }
    ]
  : []

var appSettings = concat(coreAppSettings, signalRAppSettings, emailAppSettings)

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: functionsLinuxFxVersion
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: appSettings
    }
  }
}

output functionAppName string = functionApp.name
output functionAppDefaultHostName string = functionApp.properties.defaultHostName
