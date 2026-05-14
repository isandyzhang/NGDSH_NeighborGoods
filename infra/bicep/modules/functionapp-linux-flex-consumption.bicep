targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Resource name prefix')
param namePrefix string

@description('Environment name')
param environmentName string = 'prod'

@description('Function App 名稱後綴（預設 -flex 以與舊 Consumption 同名資源並存；改為空字串且先刪舊站可沿用 func 名）')
param functionsResourceNameSuffix string = '-flex'

@description('Flex 執行階段 dotnet-isolated 版本（例 10.0）')
param functionsIsolatedRuntimeVersion string = '10.0'

@description('Flex 單一函式應用程式記憶體上限（MB）')
@allowed([2048, 4096])
param functionsFlexInstanceMemoryMB int = 2048

@description('Flex 向外擴張執行個體上限')
@minValue(40)
@maxValue(1000)
param functionsFlexMaximumInstanceCount int = 100

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

var functionAppName = '${namePrefix}-${environmentName}-func${functionsResourceNameSuffix}'
var hostingPlanName = '${namePrefix}-${environmentName}-funcflexplan'
var storageSuffix = toLower(uniqueString(resourceGroup().id, namePrefix, environmentName, 'funcjobsflex'))
var funcStorageAccountName = toLower(take('ngfx${replace('${namePrefix}${environmentName}${storageSuffix}', '-', '')}', 24))
var deploymentContainerName = 'deployments'

var injectEmail = !empty(emailConnectionString)

// 與官方 Flex quickstart 一致：https://learn.microsoft.com/azure/azure-functions/functions-create-first-function-bicep
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageQueueDataContributorId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataContributorId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

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
    allowSharedKeyAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource funcStorageBlob 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: funcStorage
  name: 'default'
  properties: {
    deleteRetentionPolicy: {}
  }
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: funcStorageBlob
  name: deploymentContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource functionsUai 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: take('uai-${namePrefix}-${environmentName}-funcflex', 128)
  location: location
}

resource roleBlobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, funcStorage.id, functionsUai.id, 'StorageBlobDataOwner')
  scope: funcStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: functionsUai.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, funcStorage.id, functionsUai.id, 'StorageBlobDataContributor')
  scope: funcStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: functionsUai.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleQueueContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, funcStorage.id, functionsUai.id, 'StorageQueueDataContributor')
  scope: funcStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributorId)
    principalId: functionsUai.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource roleTableContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, funcStorage.id, functionsUai.id, 'StorageTableDataContributor')
  scope: funcStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributorId)
    principalId: functionsUai.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: hostingPlanName
  location: location
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${functionsUai.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${funcStorage.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: functionsUai.id
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: functionsFlexMaximumInstanceCount
        instanceMemoryMB: functionsFlexInstanceMemoryMB
      }
      runtime: {
        name: 'dotnet-isolated'
        version: functionsIsolatedRuntimeVersion
      }
    }
  }
  dependsOn: [
    deploymentContainer
    roleBlobOwner
    roleBlobContributor
    roleQueueContributor
    roleTableContributor
  ]
}

var coreAppSettings = {
  AzureWebJobsStorage__accountName: funcStorage.name
  AzureWebJobsStorage__credential: 'managedidentity'
  AzureWebJobsStorage__clientId: functionsUai.properties.clientId
  FUNCTIONS_EXTENSION_VERSION: '~4'
  FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
  SCM_DO_BUILD_DURING_DEPLOYMENT: 'false'
  ASPNETCORE_ENVIRONMENT: aspnetcoreEnvironment
  ConnectionStrings__DefaultConnection: sqlConnectionString
  LineMessagingApi__ChannelId: lineMessagingChannelId
  LineMessagingApi__ChannelAccessToken: lineMessagingChannelAccessToken
  LineMessagingApi__ChannelSecret: lineMessagingChannelSecret
  LineMessagingApi__BotId: lineMessagingBotId
  LineMessagingApi__BaseUrl: lineMessagingBaseUrl
  LineMessagingApi__WebBaseUrl: lineMessagingWebBaseUrl
  LineMessagingApi__LiffId: lineMessagingLiffId
}

var signalRAppSettings = injectSignalR && !empty(signalRConnectionString)
  ? { Azure__SignalR__ConnectionString: signalRConnectionString }
  : {}

var emailAppSettings = injectEmail
  ? {
      EmailNotification__ConnectionString: emailConnectionString
      EmailNotification__FromEmailAddress: emailFromAddress
    }
  : {}

resource functionAppSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: functionApp
  name: 'appsettings'
  properties: union(coreAppSettings, signalRAppSettings, emailAppSettings)
}

output functionAppName string = functionApp.name
output functionAppDefaultHostName string = functionApp.properties.defaultHostName
output functionsUserAssignedIdentityId string = functionsUai.id
output functionsUserAssignedIdentityClientId string = functionsUai.properties.clientId
