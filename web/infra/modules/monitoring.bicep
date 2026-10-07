// Log Analytics workspace + workspace-based Application Insights for the web app's telemetry.

@description('Name suffix for the resources.')
param name string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${name}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${name}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

@description('Application Insights connection string (not a secret; it only permits sending telemetry).')
output appInsightsConnectionString string = appInsights.properties.ConnectionString
