# AERai Seller Web

Razor Pages web app for AERai Group's Amazon seller operations, hosted at **seller.aeraigroup.com**.
Source files (Amazon SP-API reports) land untouched in **blob storage**, are parsed into a **staging**
schema, promoted into curated **core** tables by a set-based stored procedure, and surfaced through
**reporting views** on the dashboard and list pages.

```
Amazon SP-API (scheduled) ─┐
Manual upload ─────────────┴─► Blob (raw/) ──► stg tables ──► core tables ──► rpt views ──► pages
```

**Stack:** .NET 10 · ASP.NET Core Razor Pages · EF Core 10 · SQL Server 2025 (local) / Azure SQL ·
ASP.NET Core Identity · Azure Blob Storage (Azurite locally) · Azure App Service, Key Vault, Application Insights · Bicep · GitHub Actions.

## Run locally

```bash
cd web
cp .env.example .env                 # set MSSQL_SA_PASSWORD
docker compose up -d                 # SQL Server + Azurite (blob storage) + Mailpit (email inbox: http://localhost:8025)
scripts/set-local-secrets.sh          # answer "y" to the database prompt, Enter for defaults
dotnet user-secrets --project src/AERai.Web.UI set "Seed:AdminEmail" "admin@aeraigroup.com"
dotnet user-secrets --project src/AERai.Web.UI set "Seed:AdminPassword" "<12+ chars, upper, lower, digit>"
dotnet run --project src/AERai.Web.UI --launch-profile http   # http://localhost:5042
```

Development applies migrations and seeds roles, the admin, and three (disabled) Amazon sync schedules on
startup. Locally `SpApi:Mode` is **Simulated**: **Tools → Amazon sync → Run now** pulls realistic generated
reports through the whole pipeline without Amazon credentials. For real data run
`scripts/set-local-secrets.sh`: it prompts (hidden input) for the database connection (optional; defaults to the
compose SQL Server with the SA password from `.env`), the SP-API LWA client id, client secret, and refresh token,
and optionally the Amazon Ads API credentials and profile id, stores them in user-secrets
(`~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`, outside the repo), and switches local runs to Live.
Press Enter to skip any prompt; skipped values keep what is already stored. `scripts/set-local-secrets.sh --simulated`
switches back. Then sign in, go to
**Tools → Import data**, upload the files in `samples/`, and **Promote** each batch. Each batch page
shows its raw file's blob path and SHA-256, and can **Re-stage from raw file** into a new batch.

> Already running SQL Server with the equivalent `docker run --name sql2025 -v sql2025data:/var/opt/mssql …`?
> Compose uses the same volume name, so `docker rm -f sql2025 && docker compose up -d` switches to
> compose without losing data.

Containerized app as well: `docker compose --profile app up -d --build` → http://localhost:8080.

## Test

```bash
dotnet test AERai.Web.slnx                                                      # SQL tests skip
AERAI_TEST_SQL="Server=localhost,1433;User Id=sa;Password=<pw>;TrustServerCertificate=True" AERAI_TEST_BLOB="UseDevelopmentStorage=true" AERAI_TEST_MAILPIT="http://localhost:8025/" dotnet test AERai.Web.slnx
```

## Pages

| Page | Who | Reads |
|---|---|---|
| Sign in, Forgot / Reset password | anyone | `auth.*` (reset links are emailed; locally they land in Mailpit) |
| Change password | any signed-in user | `auth.*` |
| Dashboard | all roles | `rpt.vw_DailySalesBySku`, `rpt.vw_InventoryPosition`, `stg.ImportBatch` |
| Inventory | all roles | `rpt.vw_InventoryPosition` |
| Orders | all roles | `rpt.vw_OrderSummary` |
| Products & COGS | all (edit: Operator+) | `core.Product` |
| Settlements | all roles | `rpt.vw_SettlementSummary` |
| Tools → Import / Batches | Operator, Admin | `stg.*` |
| Tools → Amazon sync | Operator, Admin | `ops.*` (schedules, run history) |
| Admin → Users | Admin | `auth.*` |

Conventions and architecture: [`CLAUDE.md`](CLAUDE.md). Azure: [`infra/README.md`](infra/README.md).
