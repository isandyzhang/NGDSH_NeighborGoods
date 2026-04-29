targetScope = 'resourceGroup'

@description('Existing SQL Server name (must already exist in this resource group)')
param sqlServerName string

@description('Existing SQL Database name on the existing server')
param sqlDatabaseName string

@description('Deployment location')
param location string = resourceGroup().location

@description('Azure SQL database SKU name (DTU Basic default: Basic)')
param databaseSkuName string = 'Basic'

@description('Azure SQL database SKU tier')
param databaseTier string = 'Basic'

@description('Azure SQL database hardware family (used only by vCore model)')
param databaseFamily string = 'Gen5'

@description('Azure SQL database capacity (DTU Basic default: 5)')
param databaseCapacity int = 5

@description('Azure SQL database max size in bytes')
param databaseMaxSizeBytes int = 34359738368

@description('Azure SQL serverless minimum vCores (used only by GeneralPurpose serverless)')
param databaseMinCapacity string = '0.5'

@description('Azure SQL serverless auto-pause delay in minutes (used only by GeneralPurpose serverless)')
param databaseAutoPauseDelay int = 60

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' existing = {
  name: sqlServerName
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: union({
    name: databaseSkuName
    tier: databaseTier
    capacity: databaseCapacity
  }, databaseTier == 'GeneralPurpose' ? {
    family: databaseFamily
  } : {})
  properties: union({
    maxSizeBytes: databaseMaxSizeBytes
  }, databaseTier == 'GeneralPurpose' ? {
    minCapacity: json(databaseMinCapacity)
    autoPauseDelay: databaseAutoPauseDelay
  } : {})
}

output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
