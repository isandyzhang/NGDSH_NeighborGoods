targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Resource name prefix')
param namePrefix string

@description('Environment name')
param environmentName string = 'prod'

var logAnalyticsName = '${namePrefix}-${environmentName}-law'

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

output logAnalyticsName string = logAnalytics.name
output logAnalyticsId string = logAnalytics.id
output customerId string = logAnalytics.properties.customerId
@secure()
output sharedKey string = listKeys(logAnalytics.id, logAnalytics.apiVersion).primarySharedKey
