// Free Azure Static Web App for the public marketing site (www/ in this repo).
//
// Content is deployed by .github/workflows/www-deploy.yml using the deployment token
// (az staticwebapp secrets list). The custom domain is bound in a second pass, after DNS exists:
//   CNAME  www  -> <defaultHostname output>

@description('Static Web App name.')
param name string

@description('Azure region. Static Web Apps support only a few regions (e.g. eastus2, centralus, westus2, westeurope).')
param location string

@description('Tags applied to the resource.')
param tags object

@description('Custom hostname, e.g. www.aeraigroup.com.')
param hostname string

@description('Bind the custom hostname. Set true only after the CNAME record exists.')
param bindCustomDomain bool = false

resource site 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    // Deployed from GitHub Actions with a token rather than Azure-managed repo integration.
    stagingEnvironmentPolicy: 'Disabled'
    allowConfigFileUpdates: true
  }
}

resource domain 'Microsoft.Web/staticSites/customDomains@2023-12-01' = if (bindCustomDomain) {
  parent: site
  name: hostname
  properties: {
    validationMethod: 'cname-delegation'
  }
}

output name string = site.name
output defaultHostname string = site.properties.defaultHostname
