targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Resource name prefix')
param namePrefix string

@description('Environment name')
param environmentName string = 'prod'

@description('Log Analytics customer ID')
param logAnalyticsCustomerId string

@description('Log Analytics shared key')
@secure()
param logAnalyticsSharedKey string

var containerEnvName = '${namePrefix}-${environmentName}-cae'

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsCustomerId
        sharedKey: logAnalyticsSharedKey
      }
    }
  }
}

output containerAppEnvironmentName string = containerAppEnvironment.name
output containerAppEnvironmentId string = containerAppEnvironment.id
