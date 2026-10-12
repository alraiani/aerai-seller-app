// Production parameters. Contains no secrets — authentication is Entra ID / managed identity only.
using 'main.bicep'

param environmentName = 'prod'
param appName = 'aerai-seller'
param customHostname = 'seller.aeraigroup.com'

// DNS records (seller CNAME, asuid.seller TXT) exist; binds the hostname and managed certificate.
param bindCustomDomain = true

// Replace with the Entra ID group that administers the database.
param sqlAdminLogin = 'AERai SQL Admins'
param sqlAdminObjectId = 'd2c78b79-23ba-42e0-9717-97ce33b0b276'
