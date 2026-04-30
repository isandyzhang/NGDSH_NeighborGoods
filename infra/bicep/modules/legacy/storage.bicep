targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Resource name prefix')
@minLength(1)
param namePrefix string

@description('Environment name')
@minLength(1)
param environmentName string = 'prod'

@description('Storage account SKU')
param storageAccountSku string = 'Standard_LRS'

@description('Allow public blob access for the image container')
param publicAccess bool = true

var suffix = toLower(uniqueString(resourceGroup().id, namePrefix, environmentName))
var storageAccountName = toLower(take('stg${replace('${namePrefix}${environmentName}${suffix}', '-', '')}', 24))
var blobContainerName = 'neighborgoods-images'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: storageAccountSku
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: publicAccess
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
    publicAccess: publicAccess ? 'Blob' : 'None'
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
output blobContainerName string = blobContainer.name
@secure()
output storageAccountConnectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
