// Key Vault (RBAC authorization) for app configuration that must stay secret. The web app's
// managed identity is granted read access to secrets; nothing else is granted here.

@description('Vault name (3-24 chars, globally unique).')
@maxLength(24)
param name string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

@description('Principal ID allowed to read secrets (the web app managed identity).')
param secretsReaderPrincipalId string

// Built-in role: Key Vault Secrets User.
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

resource secretsReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, secretsReaderPrincipalId, keyVaultSecretsUserRoleId)
  properties: {
    principalId: secretsReaderPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
  }
}

@description('Vault URI.')
output uri string = vault.properties.vaultUri
