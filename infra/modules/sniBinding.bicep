// Second phase of customDomain.bicep: switch the hostname binding to SNI SSL with the issued certificate.

@description('Web app name.')
param siteName string

@description('Bound hostname.')
param hostname string

@description('Thumbprint of the managed certificate for the hostname.')
param thumbprint string

resource site 'Microsoft.Web/sites@2024-04-01' existing = {
  name: siteName
}

resource binding 'Microsoft.Web/sites/hostNameBindings@2024-04-01' = {
  parent: site
  name: hostname
  properties: {
    siteName: siteName
    hostNameType: 'Verified'
    sslState: 'SniEnabled'
    thumbprint: thumbprint
  }
}
