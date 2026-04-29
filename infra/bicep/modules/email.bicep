targetScope = 'resourceGroup'

@description('Resource name prefix')
param namePrefix string

@description('Environment name')
param environmentName string = 'prod'

@description('Email domain name')
param emailDomainName string = 'neighborgoodstw.com'

@description('Email domain management mode')
@allowed([
  'CustomerManaged'
  'AzureManaged'
])
param emailDomainManagement string = 'CustomerManaged'

var emailServiceResourceName = '${namePrefix}-${environmentName}-email'

resource emailService 'Microsoft.Communication/emailServices@2026-03-18' = {
  name: emailServiceResourceName
  location: 'global'
  properties: {
    dataLocation: 'United States'
  }
}

resource emailDomain 'Microsoft.Communication/emailServices/domains@2026-03-18' = {
  parent: emailService
  name: emailDomainName
  location: 'global'
  properties: {
    domainManagement: emailDomainManagement
    userEngagementTracking: 'Disabled'
  }
}

output emailServiceName string = emailService.name
output communicationServiceName string = emailService.name
output emailDomainName string = emailDomain.name
@secure()
output emailConnectionString string = listKeys(emailService.id, emailService.apiVersion).primaryConnectionString
