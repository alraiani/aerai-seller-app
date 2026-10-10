// Production parameters. Contains no secrets — authentication is Entra ID / managed identity only.
using 'main.bicep'

param environmentName = 'prod'
param appName = 'aerai-seller'
param customHostname = 'seller.aeraigroup.com'

// Flip to true after creating the DNS records described in README.md, then redeploy.
param bindCustomDomain = false

// Flip to true after the www CNAME exists, then redeploy.
param bindWwwDomain = false

// Replace with the Entra ID group that administers the database.
param sqlAdminLogin = 'AERai SQL Admins'
param sqlAdminObjectId = '00000000-0000-0000-0000-000000000000'
