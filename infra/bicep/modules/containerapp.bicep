targetScope = 'resourceGroup'

@description('Deployment location')
param location string = resourceGroup().location

@description('Resource name prefix')
param namePrefix string

@description('Environment name')
param environmentName string = 'prod'

@description('Container App Environment resource ID')
param containerAppEnvironmentId string

@description('Container image, e.g. ghcr.io/org/neighborgoods-web:sha')
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

@description('SQL connection string')
param sqlConnectionString string

@description('Blob connection string')
param blobConnectionString string

@description('Blob container name')
param blobContainerName string

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

@description('LINE OIDC callback full URL')
param lineOidcCallbackUrl string = ''

@description('LINE OIDC scope')
param lineOidcScope string = 'openid profile'

@description('Email connection string (optional)')
param emailConnectionString string = ''

@description('Email sender address (optional)')
param emailFromAddress string = ''

@description('Email sender display name (optional)')
param emailFromDisplayName string = 'NeighborGoods'

@description('Email logo URL (optional)')
param emailLogoUrl string = ''

@description('Email base URL (optional)')
param emailBaseUrl string = ''

@description('Azure SignalR connection string (optional)')
param signalRConnectionString string = ''

@description('App environment')
param aspnetcoreEnvironment string = 'Production'

@description('Allowed CORS origin #0')
param corsAllowedOrigin0 string = 'http://localhost:5173'

@description('Allowed CORS origin #1')
param corsAllowedOrigin1 string = ''

var containerAppName = '${namePrefix}-${environmentName}-api'
var shouldInjectSignalR = !empty(signalRConnectionString)
var shouldInjectEmail = !empty(emailConnectionString)

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnvironmentId
    configuration: {
      ingress: {
        external: true
        targetPort: containerPort
        transport: 'auto'
      }
      secrets: concat([
        {
          name: 'sql-connection'
          value: sqlConnectionString
        }
        {
          name: 'blob-connection'
          value: blobConnectionString
        }
        {
          name: 'jwt-signing-key'
          value: jwtSigningKey
        }
        {
          name: 'line-oidc-channel-secret'
          value: lineOidcChannelSecret
        }
        {
          name: 'line-messaging-channel-access-token'
          value: lineMessagingChannelAccessToken
        }
        {
          name: 'line-messaging-channel-secret'
          value: lineMessagingChannelSecret
        }
      ], shouldInjectSignalR ? [
        {
          name: 'signalr-connection'
          value: signalRConnectionString
        }
      ] : [], shouldInjectEmail ? [
        {
          name: 'email-connection'
          value: emailConnectionString
        }
      ] : [])
    }
    template: {
      containers: [
        {
          name: 'web'
          image: containerImage
          resources: {
            cpu: json(containerCpu)
            memory: containerMemory
          }
          env: concat([
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: aspnetcoreEnvironment
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://0.0.0.0:${containerPort}'
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'sql-connection'
            }
            {
              name: 'AzureBlob__ConnectionString'
              secretRef: 'blob-connection'
            }
            {
              name: 'AzureBlob__ContainerName'
              value: blobContainerName
            }
            {
              name: 'Line__ChannelId'
              value: lineOidcChannelId
            }
            {
              name: 'Line__ChannelSecret'
              secretRef: 'line-oidc-channel-secret'
            }
            {
              name: 'Line__CallbackUrl'
              value: lineOidcCallbackUrl
            }
            {
              name: 'Line__Scope'
              value: lineOidcScope
            }
            {
              name: 'Jwt__Issuer'
              value: jwtIssuer
            }
            {
              name: 'Jwt__Audience'
              value: jwtAudience
            }
            {
              name: 'Jwt__SigningKey'
              secretRef: 'jwt-signing-key'
            }
            {
              name: 'Jwt__AccessTokenMinutes'
              value: string(jwtAccessTokenMinutes)
            }
            {
              name: 'Jwt__RefreshTokenDays'
              value: string(jwtRefreshTokenDays)
            }
            {
              name: 'Cors__AllowedOrigins__0'
              value: corsAllowedOrigin0
            }
            {
              name: 'Cors__AllowedOrigins__1'
              value: corsAllowedOrigin1
            }
            {
              name: 'LineMessagingApi__ChannelId'
              value: lineMessagingChannelId
            }
            {
              name: 'LineMessagingApi__ChannelAccessToken'
              secretRef: 'line-messaging-channel-access-token'
            }
            {
              name: 'LineMessagingApi__ChannelSecret'
              secretRef: 'line-messaging-channel-secret'
            }
            {
              name: 'LineMessagingApi__BotId'
              value: lineMessagingBotId
            }
            {
              name: 'LineMessagingApi__BaseUrl'
              value: lineMessagingBaseUrl
            }
            {
              name: 'EmailNotification__FromEmailAddress'
              value: emailFromAddress
            }
            {
              name: 'EmailNotification__FromDisplayName'
              value: emailFromDisplayName
            }
            {
              name: 'EmailNotification__LogoUrl'
              value: emailLogoUrl
            }
            {
              name: 'EmailNotification__BaseUrl'
              value: emailBaseUrl
            }
          ], shouldInjectSignalR ? [
            {
              name: 'Azure__SignalR__ConnectionString'
              secretRef: 'signalr-connection'
            }
          ] : [], shouldInjectEmail ? [
            {
              name: 'EmailNotification__ConnectionString'
              secretRef: 'email-connection'
            }
          ] : [])
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output containerAppName string = containerApp.name
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
