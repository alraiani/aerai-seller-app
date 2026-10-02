// AERai Seller web app — Azure environment.
//
// Provisions (resource-group scope): Log Analytics + Application Insights, Key Vault (RBAC),
// a Storage account for the raw landing zone (source files before staging),
// Azure SQL (Entra-only auth), a Linux App Service plan + web app with a system-assigned managed
// identity, and — once DNS is in place — the seller.aeraigroup.com custom domain with a free
// App Service managed certificate.
//
// Deploy:  az deployment group create -g <rg> -f main.bicep -p main.bicepparam
// See README.md for the one-time manual steps (DNS records, SQL user for the managed identity).

targetScope = 'resourceGroup'

@description('Short environment name used in resource names, e.g. prod or staging.')
@allowed(['prod', 'staging', 'dev'])
param environmentName string = 'prod'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Base name for resources. Must be globally unique enough for the web app and SQL server names.')
@minLength(3)
@maxLength(16)
param appName string = 'aerai-seller'

@description('Custom hostname served by the web app.')
param customHostname string = 'seller.aeraigroup.com'

@description('Bind the custom hostname and certificate. Set true only after the DNS records in README.md exist.')
param bindCustomDomain bool = false

@description('Display name of the Entra ID user or group that administers Azure SQL (e.g. an "AERai SQL Admins" group).')
param sqlAdminLogin string

@description('Object ID of that Entra ID user or group.')
param sqlAdminObjectId string

@description('App Service plan SKU.')
param appServiceSku string = 'B1'

@description('Tags applied to every resource.')
param tags object = {
  application: 'aerai-seller-web'
  environment: environmentName
  domain: 'aeraigroup.com'
}

var resourceSuffix = '${appName}-${environmentName}'

// Key Vault names are limited to 24 characters, so the hyphens are dropped from the suffix.
var keyVaultName = 'kv-${take(replace(resourceSuffix, '-', ''), 21)}'

// Storage account names: 3-24 lowercase alphanumerics only.
var storageAccountName = 'st${take(replace(resourceSuffix, '-', ''), 22)}'

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    name: resourceSuffix
    location: location
    tags: tags
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    serverName: 'sql-${resourceSuffix}'
    databaseName: 'AERaiSeller'
    location: location
    tags: tags
    adminLogin: sqlAdminLogin
    adminObjectId: sqlAdminObjectId
  }
}

module web 'modules/appService.bicep' = {
  name: 'web'
  params: {
    planName: 'plan-${resourceSuffix}'
    siteName: 'app-${resourceSuffix}'
    location: location
    tags: tags
    skuName: appServiceSku
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    keyVaultUri: 'https://${keyVaultName}${environment().suffixes.keyvaultDns}/'
    rawStorageServiceUri: 'https://${storageAccountName}.blob.${environment().suffixes.storage}/'
    sqlConnectionString: 'Server=tcp:${sql.outputs.serverFqdn},1433;Database=${sql.outputs.databaseName};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;'
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    name: keyVaultName
    location: location
    tags: tags
    secretsReaderPrincipalId: web.outputs.principalId
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    name: storageAccountName
    location: location
    tags: tags
    containerName: 'raw'
    blobContributorPrincipalId: web.outputs.principalId
  }
}

module customDomain 'modules/customDomain.bicep' = if (bindCustomDomain) {
  name: 'customDomain'
  params: {
    siteName: web.outputs.siteName
    planId: web.outputs.planId
    location: location
    hostname: customHostname
  }
}

@description('Default *.azurewebsites.net hostname (target of the DNS CNAME).')
output defaultHostname string = web.outputs.defaultHostname

@description('Value for the asuid TXT record that proves domain ownership.')
output customDomainVerificationId string = web.outputs.customDomainVerificationId

@description('Web app name (for deployment and the SQL CREATE USER statement).')
output webAppName string = web.outputs.siteName

@description('SQL server name, for CI firewall rules (AZURE_SQL_SERVER).')
output sqlServerName string = sql.outputs.serverName

@description('SQL server fully qualified domain name.')
output sqlServerFqdn string = sql.outputs.serverFqdn

@description('Raw landing-zone blob endpoint.')
output rawStorageBlobEndpoint string = storage.outputs.blobEndpoint

@description('Key Vault URI.')
output keyVaultUri string = keyVault.outputs.uri
