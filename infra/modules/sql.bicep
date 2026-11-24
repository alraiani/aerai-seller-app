// Azure SQL logical server + database with Microsoft Entra-only authentication: there is no SQL
// login or password anywhere. The web app connects with its managed identity (see README.md for
// the one-time CREATE USER step) and CI applies migrations as a member of the admin group.

@description('Logical server name (globally unique).')
param serverName string

@description('Database name.')
param databaseName string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

@description('Display name of the Entra ID admin user or group.')
param adminLogin string

@description('Object ID of the Entra ID admin user or group.')
param adminObjectId string

resource server 'Microsoft.Sql/servers@2023-08-01' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      sid: adminObjectId
      tenantId: subscription().tenantId
      principalType: 'Group'
    }
  }
}

// 0.0.0.0 is Azure's convention for "allow Azure services" (App Service outbound, GitHub-hosted
// runners via azure/login are NOT covered — CI adds a temporary rule for its own IP).
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: server
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: server
  name: databaseName
  location: location
  tags: tags
  sku: {
    // Standard S0 (10 DTUs): a small fixed-price tier, always on. Set to match the live database, which
    // was moved from serverless to S0 on 2026-11-05. The pages stay fast on it because the heavy
    // settlement work runs at import time (rpt summary tables); step up to S1/S2 before serverless.
    // Azure stores S0 as name 'Standard' with capacity 10 DTUs; written that way so a deploy shows no change.
    name: 'Standard'
    tier: 'Standard'
    capacity: 10
  }
  properties: {
    requestedBackupStorageRedundancy: 'Local'
  }
}

@description('Server FQDN.')
output serverFqdn string = server.properties.fullyQualifiedDomainName

@description('Database name.')
output databaseName string = database.name

@description('Server name (without the DNS suffix).')
output serverName string = server.name
