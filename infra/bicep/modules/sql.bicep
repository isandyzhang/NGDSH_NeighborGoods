targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Resource name prefix')
param namePrefix string

@description('Environment name')
param environmentName string = 'prod'

@description('Azure SQL admin login')
param adminLogin string

@secure()
@description('Azure SQL admin password')
param adminPassword string

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

@description('Azure SQL database serverless minimum vCores')
param databaseMinCapacity string = '0.5'

@description('Azure SQL serverless auto-pause delay in minutes')
param databaseAutoPauseDelay int = 60

var suffix = toLower(uniqueString(resourceGroup().id, namePrefix, environmentName))
var sqlServerName = '${namePrefix}-${environmentName}-sql-${take(suffix, 6)}'
var sqlDatabaseName = '${namePrefix}-${environmentName}-db'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  name: sqlDatabaseName
  parent: sqlServer
  location: location
  sku: {
    name: databaseSkuName
    tier: databaseTier
    family: databaseFamily
    capacity: databaseCapacity
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: databaseMaxSizeBytes
    minCapacity: json(databaseMinCapacity)
    autoPauseDelay: databaseAutoPauseDelay
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

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
@secure()
output sqlConnectionString string = 'Server=tcp:${sqlServer.name}.database.windows.net,1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${adminLogin};Password=${adminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
