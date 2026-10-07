# Azure infrastructure — seller.aeraigroup.com

Bicep for the AERai web app's Azure environment. Nothing here contains secrets: the web app reaches
Azure SQL, Blob Storage, and Key Vault through its **system-assigned managed identity**, and CI authenticates with
**OIDC federated credentials**.

| Module | Provisions |
|---|---|
| `modules/monitoring.bicep` | Log Analytics workspace + Application Insights |
| `modules/sql.bicep` | Azure SQL server (Entra-only auth) + serverless `AERaiSeller` database |
| `modules/appService.bicep` | Linux App Service plan + .NET 10 web app (HTTPS-only, `/healthz` health check, basic-auth publishing off) |
| `modules/storage.bicep` | Storage account (Entra-only, no shared keys) + private `raw` container: the landing zone for source files, with soft delete, versioning, and Cool/Archive tiering |
| `modules/keyVault.bicep` | Key Vault (RBAC) + *Key Vault Secrets User* for the web app identity |
| `modules/customDomain.bicep` | `seller.aeraigroup.com` binding + free managed certificate (opt-in) |

## First-time setup

1. **Entra ID admin group for SQL.** Create a group (e.g. *AERai SQL Admins*), add the people who
   administer the database and the CI deployment identity, and put its name/object id in `main.bicepparam`.
2. **Deploy.**
   ```bash
   az group create -n rg-aerai-seller-prod -l eastus2
   az deployment group create -g rg-aerai-seller-prod -f main.bicep -p main.bicepparam
   ```
3. **Grant the app identity database access.** Connected to `AERaiSeller` as an admin-group member,
   run `sql/grant-app-identity.sql` (replace the name with the `webAppName` output).
4. **DNS for aeraigroup.com** (at whichever provider hosts the zone):

   | Type | Name | Value |
   |---|---|---|
   | CNAME | `seller` | `<defaultHostname output>` (e.g. `app-aerai-seller-prod.azurewebsites.net`) |
   | TXT | `asuid.seller` | `<customDomainVerificationId output>` |

5. **Bind the domain.** Once DNS resolves, set `bindCustomDomain = true` and redeploy. The managed
   certificate is issued and renewed automatically by App Service.
6. **Connect Amazon SP-API.** Add three Key Vault secrets — the app reads them through its managed
   identity (`--` maps to `:` in configuration):

   | Secret | Value |
   |---|---|
   | `SpApi--ClientId` | LWA client id of the SP-API app |
   | `SpApi--ClientSecret` | LWA client secret |
   | `SpApi--RefreshToken` | Seller authorization refresh token for North America (US, Canada) |
   | `SpApi--Europe--RefreshToken` | *Optional.* Refresh token for the Europe region (UK), once the UK account is authorized |

   Restart the app. The **Amazon sync** page shows "Live" with no warning once the first three are present;
   then switch on the schedules you want. Secret rotation = update the secret and restart.
7. **Email for password resets.** Any SMTP provider works; with Azure Communication Services
   (Email), verify the `aeraigroup.com` domain there, then set `emailHost = 'smtp.azurecomm.net'`,
   `emailUserName`, and `emailFromAddress` in `main.bicepparam` and add the SMTP password as the Key
   Vault secret `Email--Password`. Emailed links use `App__PublicBaseUrl` (the custom domain once
   `bindCustomDomain = true`), never the request's Host header.
8. **Seed the first administrator** (optional): add Key Vault secrets `Seed--AdminEmail` and
   `Seed--AdminPassword`, restart the app, sign in, then **delete both secrets**.

## CI/CD

`.github/workflows/web-ci.yml` builds, tests (including SQL integration tests against a SQL Server
service container), and validates this Bicep on every change under `web/`. Pushes to `main` deploy
through the `production` environment (configure required reviewers on it in GitHub):

1. Apply EF Core migrations with a self-contained migrations bundle, authenticating as the CI identity.
2. Deploy the published app to App Service.

Required GitHub configuration (no secrets — these are identifiers):

| Kind | Name | Value |
|---|---|---|
| Variable | `AZURE_CLIENT_ID` | App registration / user-assigned identity with a federated credential for this repo's `production` environment |
| Variable | `AZURE_TENANT_ID` | Entra tenant id |
| Variable | `AZURE_SUBSCRIPTION_ID` | Subscription id |
| Variable | `AZURE_RESOURCE_GROUP` | e.g. `rg-aerai-seller-prod` |
| Variable | `AZURE_WEBAPP_NAME` | `webAppName` output |
| Variable | `AZURE_SQL_SERVER` | `sqlServerName` (without `.database.windows.net`) |

The CI identity needs *Website Contributor* on the web app, *SQL Server Contributor* on the server
(to open a temporary firewall rule for the runner), and membership in the SQL admin group.
