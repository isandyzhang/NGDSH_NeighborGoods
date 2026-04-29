targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Resource name prefix')
param namePrefix string

@description('Environment name')
param environmentName string = 'prod'

@description('Static Web App SKU. Free is recommended for low-cost start.')
@allowed([
  'Free'
  'Standard'
])
param staticWebAppSku string = 'Free'

@description('GitHub repository URL (for linking CI/CD in Static Web Apps)')
param repositoryUrl string

@description('Repository branch to build and deploy')
param repositoryBranch string = 'main'

@description('Frontend app location in repository')
param appLocation string = 'NeighborGoods.Frontend'

@description('Build output location relative to appLocation')
param outputLocation string = 'dist'

var suffix = toLower(uniqueString(resourceGroup().id, namePrefix, environmentName))
var staticWebAppName = '${namePrefix}-${environmentName}-swa-${take(suffix, 6)}'

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: staticWebAppSku
    tier: staticWebAppSku
  }
  properties: {
    repositoryUrl: repositoryUrl
    branch: repositoryBranch
    buildProperties: {
      appLocation: appLocation
      outputLocation: outputLocation
    }
  }
}

output staticWebAppName string = staticWebApp.name
output staticWebAppDefaultHostname string = staticWebApp.properties.defaultHostname
