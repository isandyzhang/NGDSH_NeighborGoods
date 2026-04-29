targetScope = 'resourceGroup'

@description('Existing Storage Account name (must already exist in this resource group)')
param storageAccountName string

@description('Blob container name to manage on the existing storage account')
param blobContainerName string = 'neighborgoods-images'

@description('Allow public blob access (Blob = anonymous read of blobs)')
param publicAccess bool = true

resource stg 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource blobSvc 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: stg
  name: 'default'
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobSvc
  name: blobContainerName
  properties: {
    publicAccess: publicAccess ? 'Blob' : 'None'
  }
}

output storageAccountName string = stg.name
output blobContainerName string = blobContainer.name
