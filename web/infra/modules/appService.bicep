// Linux App Service plan + .NET 10 web app with a system-assigned managed identity.
// The identity is what the app uses for Azure SQL, Blob Storage, and Key Vault — no credentials in app settings.

@description('App Service plan name.')
param planName string

@description('Web app name (becomes <name>.azurewebsites.net).')
param siteName string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

@description('Plan SKU, e.g. B1, P0v3.')
param skuName string

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('Key Vault URI the app loads additional configuration from.')
param keyVaultUri string

@description('Blob service endpoint of the raw landing zone (accessed with the managed identity).')
param rawStorageServiceUri string

@description('SQL connection string using managed identity authentication (contains no secret).')
param sqlConnectionString string

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'linux'
  sku: { name: skuName }
  properties: {
    reserved: true // Required for Linux plans.
  }
}

resource site 'Microsoft.Web/sites@2024-04-01' = {
  name: siteName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/healthz'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        // App Service terminates TLS at its front end; this makes the app honor X-Forwarded-Proto.
        { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'KeyVault__Uri', value: keyVaultUri }
        { name: 'RawStorage__ServiceUri', value: rawStorageServiceUri }
        { name: 'RawStorage__ContainerName', value: 'raw' }
        // SP-API credentials are Key Vault secrets (SpApi--ClientId, SpApi--ClientSecret, SpApi--RefreshToken), never app settings.
        { name: 'SpApi__Mode', value: 'Live' }
      ]
      connectionStrings: [
        { name: 'Sql', connectionString: sqlConnectionString, type: 'SQLAzure' }
      ]
    }
  }
}

// Basic auth publishing credentials are disabled; deployments use Entra ID (azure/login OIDC).
resource scmBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: site
  name: 'scm'
  properties: { allow: false }
}

resource ftpBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: site
  name: 'ftp'
  properties: { allow: false }
}

@description('Web app name.')
output siteName string = site.name

@description('App Service plan resource ID.')
output planId string = plan.id

@description('Default hostname.')
output defaultHostname string = site.properties.defaultHostName

@description('Domain verification ID for the asuid TXT record.')
output customDomainVerificationId string = site.properties.customDomainVerificationId

@description('Principal ID of the web app managed identity.')
output principalId string = site.identity.principalId
