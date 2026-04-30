targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Resource name prefix')
param namePrefix string = 'neighborgoods'

@description('Environment name')
param environmentName string = 'prod'

@description('Deploy Azure SignalR Service or not')
param deploySignalR bool = true

@description('Provision SQL resources in this resource group (uses modules/legacy/sql.bicep)')
param provisionSqlResources bool = true

@description('Provision Storage resources in this resource group (uses modules/legacy/storage.bicep)')
param provisionStorageResources bool = true

@description('Manage existing SQL resources (server/database) in this resource group via IaC')
param manageExistingSql bool = false

@description('Manage existing Storage resources (account/container) in this resource group via IaC')
param manageExistingStorage bool = false

@description('Existing SQL Server name (used when manageExistingSql=true)')
param existingSqlServerName string = ''

@description('Existing SQL Database name (used when manageExistingSql=true)')
param existingSqlDatabaseName string = ''

@description('Existing Storage Account name (used when manageExistingStorage=true)')
param existingStorageAccountName string = ''

@description('Existing SQL connection string injected to Container App when not provisioning SQL')
param existingSqlConnectionString string = ''

@description('Existing Blob connection string injected to Container App when not provisioning Storage')
param existingBlobConnectionString string = ''

@description('Existing Blob container name')
param existingBlobContainerName string = ''

@description('Deploy Email resources in this resource group')
param deployEmailResources bool = false

@description('Azure SQL admin login')
param sqlAdminLogin string = 'ngadmin'

@secure()
@description('Azure SQL admin password')
param sqlAdminPassword string = ''

@description('Azure SQL database SKU name (Serverless default: GP_S_Gen5_1)')
param databaseSkuName string = 'GP_S_Gen5_1'

@description('Azure SQL database SKU tier')
param databaseTier string = 'GeneralPurpose'

@description('Azure SQL database hardware family')
param databaseFamily string = 'Gen5'

@description('Azure SQL database vCore capacity')
param databaseCapacity int = 1

@description('Azure SQL database max size in bytes')
param databaseMaxSizeBytes int = 34359738368

@description('Azure SQL serverless minimum vCores')
param databaseMinCapacity string = '0.5'

@description('Azure SQL serverless auto-pause delay in minutes')
param databaseAutoPauseDelay int = 60

@description('Allow public blob access on the managed container (used when manageExistingStorage=true)')
param manageExistingStoragePublicAccess bool = true

@description('LINE OIDC ChannelId')
param lineOidcChannelId string

@secure()
@description('LINE OIDC ChannelSecret')
param lineOidcChannelSecret string

@description('JWT issuer')
param jwtIssuer string = 'NeighborGoods.Api'

@description('JWT audience')
param jwtAudience string = 'NeighborGoods.Api.Client'

@secure()
@description('JWT signing key')
param jwtSigningKey string

@description('JWT access token minutes')
param jwtAccessTokenMinutes int = 30

@description('JWT refresh token days')
param jwtRefreshTokenDays int = 14

@description('LINE Messaging API ChannelId (optional)')
param lineMessagingChannelId string = ''

@secure()
@description('LINE Messaging API ChannelAccessToken (optional)')
param lineMessagingChannelAccessToken string = ''

@secure()
@description('LINE Messaging API ChannelSecret (optional)')
param lineMessagingChannelSecret string = ''

@description('LINE Messaging API BotId (optional)')
param lineMessagingBotId string = ''

@description('LINE Messaging API BaseUrl (optional)')
param lineMessagingBaseUrl string = ''

@description('Email sender address (optional)')
param emailFromAddress string = ''

@description('Email sender display name (optional)')
param emailFromDisplayName string = 'NeighborGoods'

@description('Email logo URL (optional)')
param emailLogoUrl string = ''

@description('Email base URL (optional)')
param emailBaseUrl string = ''

@description('Line OIDC callback full URL')
param lineOidcCallbackUrl string = ''

@description('Line OIDC scope')
param lineOidcScope string = 'openid profile'

@description('App environment')
param aspnetcoreEnvironment string = 'Production'

@description('Allowed CORS origin #0')
param corsAllowedOrigin0 string = 'http://localhost:5173'

@description('Allowed CORS origin #1')
param corsAllowedOrigin1 string = ''

@description('Container image')
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

@description('Static Web App SKU')
@allowed([
  'Free'
  'Standard'
])
param staticWebAppSku string = 'Free'

@description('GitHub repository URL')
param repositoryUrl string

@description('Repository branch to build and deploy')
param repositoryBranch string = 'main'

@description('Frontend app location in repository')
param appLocation string = 'NeighborGoods.Frontend'

@description('Build output location relative to appLocation')
param outputLocation string = 'dist'

module storage 'modules/legacy/storage.bicep' = if (provisionStorageResources) {
  name: 'storage-module'
  params: {
    location: location
    namePrefix: namePrefix
    environmentName: environmentName
    publicAccess: true
  }
}

module sql 'modules/legacy/sql.bicep' = if (provisionSqlResources) {
  name: 'sql-module'
  params: {
    location: location
    namePrefix: namePrefix
    environmentName: environmentName
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
    databaseSkuName: databaseSkuName
    databaseTier: databaseTier
    databaseFamily: databaseFamily
    databaseCapacity: databaseCapacity
    databaseMaxSizeBytes: databaseMaxSizeBytes
    databaseMinCapacity: databaseMinCapacity
    databaseAutoPauseDelay: databaseAutoPauseDelay
  }
}

module sqlManage 'modules/sql-manage-existing.bicep' = if (manageExistingSql && !provisionSqlResources) {
  name: 'sql-manage-existing-module'
  params: {
    location: location
    sqlServerName: existingSqlServerName
    sqlDatabaseName: existingSqlDatabaseName
    databaseSkuName: databaseSkuName
    databaseTier: databaseTier
    databaseFamily: databaseFamily
    databaseCapacity: databaseCapacity
    databaseMaxSizeBytes: databaseMaxSizeBytes
    databaseMinCapacity: databaseMinCapacity
    databaseAutoPauseDelay: databaseAutoPauseDelay
  }
}

module storageManage 'modules/storage-manage-existing.bicep' = if (manageExistingStorage && !provisionStorageResources) {
  name: 'storage-manage-existing-module'
  params: {
    storageAccountName: existingStorageAccountName
    blobContainerName: existingBlobContainerName
    publicAccess: manageExistingStoragePublicAccess
  }
}

module signalr 'modules/signalr.bicep' = if (deploySignalR) {
  name: 'signalr-module'
  params: {
    location: location
    namePrefix: namePrefix
    environmentName: environmentName
  }
}

module logging 'modules/logging.bicep' = {
  name: 'logging-module'
  params: {
    location: location
    namePrefix: namePrefix
    environmentName: environmentName
  }
}

module containerAppEnvironment 'modules/containerappenvironment.bicep' = {
  name: 'containerappenv-module'
  params: {
    location: location
    namePrefix: namePrefix
    environmentName: environmentName
    logAnalyticsCustomerId: logging.outputs.customerId
    logAnalyticsSharedKey: logging.outputs.sharedKey
  }
}

module email 'modules/email.bicep' = if (deployEmailResources) {
  name: 'email-module'
  params: {
    namePrefix: namePrefix
    environmentName: environmentName
    emailDomainName: 'neighborgoodstw.com'
    emailDomainManagement: 'CustomerManaged'
  }
}

module containerapp 'modules/containerapp.bicep' = {
  name: 'containerapp-module'
  params: {
    location: location
    namePrefix: namePrefix
    environmentName: environmentName
    containerAppEnvironmentId: containerAppEnvironment.outputs.containerAppEnvironmentId
    containerImage: containerImage
    containerPort: containerPort
    containerCpu: containerCpu
    containerMemory: containerMemory
    minReplicas: minReplicas
    maxReplicas: maxReplicas
    #disable-next-line BCP318
    sqlConnectionString: provisionSqlResources ? sql.outputs.sqlConnectionString : existingSqlConnectionString
    #disable-next-line BCP318
    blobConnectionString: provisionStorageResources ? storage.outputs.storageAccountConnectionString : existingBlobConnectionString
    blobContainerName: provisionStorageResources ? storage!.outputs.blobContainerName : existingBlobContainerName
    lineOidcChannelId: lineOidcChannelId
    lineOidcChannelSecret: lineOidcChannelSecret
    jwtIssuer: jwtIssuer
    jwtAudience: jwtAudience
    jwtSigningKey: jwtSigningKey
    jwtAccessTokenMinutes: jwtAccessTokenMinutes
    jwtRefreshTokenDays: jwtRefreshTokenDays
    lineMessagingChannelId: lineMessagingChannelId
    lineMessagingChannelAccessToken: lineMessagingChannelAccessToken
    lineMessagingChannelSecret: lineMessagingChannelSecret
    lineMessagingBotId: lineMessagingBotId
    lineMessagingBaseUrl: lineMessagingBaseUrl
    lineOidcCallbackUrl: lineOidcCallbackUrl
    lineOidcScope: lineOidcScope
    #disable-next-line BCP318
    emailConnectionString: deployEmailResources ? email.outputs.emailConnectionString : ''
    emailFromAddress: emailFromAddress
    emailFromDisplayName: emailFromDisplayName
    emailLogoUrl: emailLogoUrl
    emailBaseUrl: emailBaseUrl
    #disable-next-line BCP318
    signalRConnectionString: deploySignalR ? signalr.outputs.signalRConnectionString : ''
    aspnetcoreEnvironment: aspnetcoreEnvironment
    corsAllowedOrigin0: corsAllowedOrigin0
    corsAllowedOrigin1: corsAllowedOrigin1
  }
}

module staticwebapp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp-module'
  params: {
    location: location
    namePrefix: namePrefix
    environmentName: environmentName
    staticWebAppSku: staticWebAppSku
    repositoryUrl: repositoryUrl
    repositoryBranch: repositoryBranch
    appLocation: appLocation
    outputLocation: outputLocation
  }
}

output deploymentSummary object = {
  resourceGroup: resourceGroup().name
  location: location
  environment: environmentName
  backend: {
    containerAppName: containerapp.outputs.containerAppName
    containerAppUrl: containerapp.outputs.containerAppUrl
  }
  frontend: {
    staticWebAppName: staticwebapp.outputs.staticWebAppName
    staticWebAppHostName: staticwebapp.outputs.staticWebAppDefaultHostname
  }
}
