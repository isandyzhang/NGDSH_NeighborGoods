targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Environment name')
param environmentName string = 'prod'

@description('Resource name prefix')
param namePrefix string = 'neighborgoods'

@description('Container image, e.g. ghcr.io/org/neighborgoods-web:sha')
param containerImage string

@description('Container app target port')
param containerPort int = 8080

@description('Container CPU')
param containerCpu string = '0.5'

@description('Container memory')
param containerMemory string = '1.0Gi'

@description('Minimum replica count')
param minReplicas int = 0

@description('Maximum replica count')
param maxReplicas int = 1

@description('Deploy Azure SignalR Service or not')
param deploySignalR bool = true

@description('Provision a new Azure SQL server/database in this resource group')
param provisionSqlResources bool = true

@description('Provision a new Azure Storage account/container in this resource group')
param provisionStorageResources bool = true

@description('Azure SQL admin login (used only when provisionSqlResources = true)')
param sqlAdminLogin string = ''

@secure()
@description('Azure SQL admin password (used only when provisionSqlResources = true)')
param sqlAdminPassword string = ''

@secure()
@description('Existing SQL connection string (used when provisionSqlResources = false)')
param existingSqlConnectionString string = ''

@secure()
@description('Existing Azure Blob connection string (used when provisionStorageResources = false)')
param existingBlobConnectionString string = ''

@description('Existing blob container name (used when provisionStorageResources = false)')
param existingBlobContainerName string = ''

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
var storageAccountName = toLower(take(replace('${namePrefix}${environmentName}${suffix}', '-', ''), 24))
var blobContainerName = 'neighborgoods-images'
var sqlServerName = '${namePrefix}-${environmentName}-sql-${take(suffix, 6)}'
var sqlDatabaseName = '${namePrefix}-${environmentName}-db'
var logAnalyticsName = '${namePrefix}-${environmentName}-law'
var containerEnvName = '${namePrefix}-${environmentName}-cae'
var containerAppName = '${namePrefix}-${environmentName}-api'
var signalRName = '${namePrefix}-${environmentName}-signalr-${take(suffix, 6)}'

var sqlConnectionSecretValue = provisionSqlResources
  ? 'Server=tcp:${sqlServer.name}.database.windows.net,1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  : existingSqlConnectionString

var blobConnectionSecretValue = provisionStorageResources
  ? 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
  : existingBlobConnectionString

var blobContainerEnvValue = provisionStorageResources ? blobContainer.name : existingBlobContainerName

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: listKeys(logAnalytics.id, logAnalytics.apiVersion).primarySharedKey
      }
    }
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = if (provisionStorageResources) {
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

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = if (provisionStorageResources) {
  name: 'default'
  parent: storageAccount
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = if (provisionStorageResources) {
  name: blobContainerName
  parent: blobService
  properties: {
    publicAccess: 'Blob'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = if (provisionSqlResources) {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = if (provisionSqlResources) {
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

resource sqlFirewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = if (provisionSqlResources) {
  name: 'AllowAzureServices'
  parent: sqlServer
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
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

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: containerPort
        transport: 'auto'
      }
      secrets: [
        {
          name: 'sql-connection'
          value: sqlConnectionSecretValue
        }
        {
          name: 'blob-connection'
          value: blobConnectionSecretValue
        }
        {
          name: 'line-oidc-channel-id'
          value: lineOidcChannelId
        }
        {
          name: 'line-oidc-channel-secret'
          value: lineOidcChannelSecret
        }
        {
          name: 'line-msg-token'
          value: lineMessagingAccessToken
        }
        {
          name: 'line-msg-secret'
          value: lineMessagingChannelSecret
        }
        {
          name: 'email-connection'
          value: emailConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: containerImage
          resources: {
            cpu: json(containerCpu)
            memory: containerMemory
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: aspnetcoreEnvironment
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://0.0.0.0:${containerPort}'
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'sql-connection'
            }
            {
              name: 'AzureBlob__ConnectionString'
              secretRef: 'blob-connection'
            }
            {
              name: 'AzureBlob__ContainerName'
              value: blobContainerEnvValue
            }
            {
              name: 'Authentication__Line__ChannelId'
              secretRef: 'line-oidc-channel-id'
            }
            {
              name: 'Authentication__Line__ChannelSecret'
              secretRef: 'line-oidc-channel-secret'
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
              secretRef: 'line-msg-token'
            }
            {
              name: 'LineMessagingApi__ChannelSecret'
              secretRef: 'line-msg-secret'
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
              secretRef: 'email-connection'
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
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output containerAppName string = containerApp.name
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output storageAccountName string = provisionStorageResources ? storageAccount.name : 'external-storage'
output sqlServerName string = provisionSqlResources ? sqlServer.name : 'external-sql'
output sqlDatabaseName string = provisionSqlResources ? sqlDatabase.name : 'external-db'
output signalRName string = deploySignalR ? signalR.name : 'not-deployed'
