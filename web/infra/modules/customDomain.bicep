// Binds a custom hostname with a free App Service managed certificate.
//
// Prerequisites (manual, at the DNS host for aeraigroup.com — see README.md):
//   CNAME  seller        -> <app>.azurewebsites.net
//   TXT    asuid.seller  -> <customDomainVerificationId output>
//
// Binding is two-phase: the hostname must exist (SSL disabled) before a managed certificate can
// be issued for it, and only then can the binding be updated to SNI with that certificate.

@description('Web app name.')
param siteName string

@description('App Service plan resource ID (required by the managed certificate).')
param planId string

@description('Azure region.')
param location string

@description('Hostname to bind, e.g. seller.aeraigroup.com.')
param hostname string

resource site 'Microsoft.Web/sites@2024-04-01' existing = {
  name: siteName
}

resource hostnameBinding 'Microsoft.Web/sites/hostNameBindings@2024-04-01' = {
  parent: site
  name: hostname
  properties: {
    siteName: siteName
    hostNameType: 'Verified'
    sslState: 'Disabled'
  }
}

resource certificate 'Microsoft.Web/certificates@2024-04-01' = {
  name: '${siteName}-${replace(hostname, '.', '-')}'
  location: location
  dependsOn: [hostnameBinding]
  properties: {
    serverFarmId: planId
    canonicalName: hostname
  }
}

// A nested module is needed to update the same binding resource a second time in one deployment.
module sniBinding 'sniBinding.bicep' = {
  name: 'sniBinding-${uniqueString(hostname)}'
  params: {
    siteName: siteName
    hostname: hostname
    thumbprint: certificate.properties.thumbprint
  }
}
