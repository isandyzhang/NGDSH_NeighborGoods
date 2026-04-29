targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Resource name prefix')
param namePrefix string

@description('Environment name')
param environmentName string = 'prod'

var suffix = toLower(uniqueString(resourceGroup().id, namePrefix, environmentName))
var signalRName = '${namePrefix}-${environmentName}-signalr-${take(suffix, 6)}'

resource signalR 'Microsoft.SignalRService/signalR@2024-03-01' = {
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

output signalRName string = signalR.name
output signalRHostName string = signalR.properties.hostName
@secure()
output signalRConnectionString string = signalR.listKeys().primaryConnectionString
