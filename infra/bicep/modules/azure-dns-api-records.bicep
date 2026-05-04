targetScope = 'resourceGroup'

@description('現有 Azure DNS 區域名稱（例 neighborgoodstw.com）')
param dnsZoneName string

@description('API 子網域在該 zone 下的相對名稱（例 api，即完整 FQDN 為 api.<dnsZoneName>）')
param apiRecordRelativeName string

@description('Container App 預設 FQDN（僅主機名，不含 https://）')
param containerAppIngressFqdn string

@description('Container App 的 customDomainVerificationId（Portal/CLI 或上層模組輸出）')
param domainVerificationId string

var cnameTargetFqdn = endsWith(containerAppIngressFqdn, '.')
  ? containerAppIngressFqdn
  : '${containerAppIngressFqdn}.'

var asuidTxtRecordSetName = 'asuid.${apiRecordRelativeName}'

resource dnsZone 'Microsoft.Network/dnsZones@2018-05-01' existing = {
  name: dnsZoneName
}

resource apiCname 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = {
  parent: dnsZone
  name: apiRecordRelativeName
  properties: {
    TTL: 300
    CNAMERecord: {
      cname: cnameTargetFqdn
    }
  }
}

resource asuidTxt 'Microsoft.Network/dnsZones/TXT@2018-05-01' = {
  parent: dnsZone
  name: asuidTxtRecordSetName
  properties: {
    TTL: 300
    TXTRecords: [
      {
        value: [
          domainVerificationId
        ]
      }
    ]
  }
}

output apiCnameFqdn string = '${apiRecordRelativeName}.${dnsZoneName}'
output asuidTxtRecordSetName string = asuidTxtRecordSetName
