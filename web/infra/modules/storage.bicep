// Storage account for the raw landing zone: untouched source files (uploads today, SP-API report
// downloads later) are written here before being parsed into the stg schema.
//
// Security: shared-key access is disabled, so the account can only be reached with Entra ID — the
// web app's managed identity gets Storage Blob Data Contributor; there is no account key to leak.
// Lifecycle: raw files move to Cool after 30 days and Archive after 180, and are never deleted
// automatically (they are the audit record of what was received).

@description('Storage account name (3-24 lowercase alphanumerics, globally unique).')
@minLength(3)
@maxLength(24)
param name string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

@description('Blob container for raw files.')
param containerName string = 'raw'

@description('Principal ID granted read/write access to blobs (the web app managed identity).')
param blobContributorPrincipalId string

// Built-in role: Storage Blob Data Contributor.
var blobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  // LRS, not ZRS: the lifecycle rule below archives old blobs, and archive tier is unsupported on ZRS accounts.
  sku: { name: 'Standard_LRS' }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: account
  name: 'default'
  properties: {
    // Protects raw files from accidental deletion or overwrite by any client.
    deleteRetentionPolicy: { enabled: true, days: 30 }
    containerDeleteRetentionPolicy: { enabled: true, days: 30 }
    isVersioningEnabled: true
  }
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: containerName
  properties: { publicAccess: 'None' }
}

resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: account
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'tier-raw-files'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: ['blockBlob']
              prefixMatch: ['${containerName}/']
            }
            actions: {
              baseBlob: {
                tierToCool: { daysAfterModificationGreaterThan: 30 }
                tierToArchive: { daysAfterModificationGreaterThan: 180 }
              }
            }
          }
        }
      ]
    }
  }
}

resource blobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: account
  name: guid(account.id, blobContributorPrincipalId, blobDataContributorRoleId)
  properties: {
    principalId: blobContributorPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
  }
}

@description('Blob service endpoint (RawStorage__ServiceUri).')
output blobEndpoint string = account.properties.primaryEndpoints.blob
