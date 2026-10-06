# AERai Seller Web (seller.aeraigroup.com)

Multi-user web app for AERai Group's Amazon seller operations: source files (Amazon SP-API reports) land untouched in blob storage, are parsed into a staging layer in a centralized SQL Server database, promotes it into curated tables, and exposes reporting views to Razor Pages (dashboard, inventory, orders, settlements, products/COGS, import tools, user admin).

**Always load the `web-coding-standards` skill before writing or reviewing code here.** It holds the principles, patterns, practices, and commenting standard this solution follows.

Work tracked on the GitHub project board (https://github.com/users/alraiani/projects/3) is logged with the `github-board-updates` skill: move the item and comment on its issue at start, each phase, and finish.

The WPF desktop app in `../src` is a **reference only** for domain concepts. This solution never references it, never builds it, and does not inherit its SQLite workarounds (`DateTimeOffset` translates fine on SQL Server). Rules in the root `CLAUDE.md` about WPF, WPF-UI, MVVM, DPAPI, and SQLite do not apply here.

## Solution layout

```
web/
  AERai.Web.slnx
  src/
    AERai.Web.Domain/          # Entities: Staging/ (raw), Core/ (curated), Reporting/ (view read models). No references.
    AERai.Web.Application/     # Abstractions, use-case services, DTOs, Result, file parsing. References Domain.
    AERai.Web.Infrastructure/  # AppDbContext, EF configs, migrations (incl. views/procs), queries, Identity adapters.
    AERai.Web.UI/              # Razor Pages + Program.cs composition root.
  tests/                       # xUnit: Application.Tests (pure), Infrastructure.Tests (needs SQL; skips without AERAI_TEST_SQL)
  infra/                       # Bicep for Azure (App Service, Azure SQL, Key Vault, App Insights, custom domain)
  samples/                     # Example import files
  docker-compose.yml           # Local SQL Server 2025 (+ optional containerized app)
```

Dependencies: `UI → Application → Domain`; `Infrastructure → Application`. `UI` references `Infrastructure` **only** from `Program.cs` (`AddInfrastructure`, `InitializeDatabaseAsync`). PageModels never touch `AppDbContext`, `UserManager`, or `SignInManager` — they call Application interfaces (`IIdentityService`, `IReportingQueries`, …).

## Data flow: source → blob → staging → core → views

```
Amazon SP-API reports ─┐  (upload today; automated download later)
                       ▼
   Blob storage  raw/{source}/{yyyy}/{MM}/{dd}/{id}-{file}   ← landing zone, write-once, kept forever
                       │  StagingImportService.StageRawFileAsync (always reads FROM blob)
                       ▼
   stg.*   raw text rows ── core.usp_PromoteImportBatch ──► core.*  typed ──► rpt.vw_*  views ──► pages
```

- **Landing zone** (`IRawFileStore`, Azure Blob Storage; Azurite locally): every accepted file is stored byte-for-byte before parsing, with its SHA-256 recorded on `stg.ImportBatch` (`RawFilePath`, `RawFileSha256`). Blobs are never overwritten.
- **Staging reads from the blob, never from the upload stream**, so uploads, re-staging (`RestageAsync`), and future machine ingestion all share one path. A future SP-API worker should: download report → `IRawFileStore.SaveAsync` (path from `RawFilePaths.Build`) → `StageRawFileAsync`.
- Files that fail parsing stay in the landing zone (audit) but create no batch.
- Path layout is an Application concern (`RawFilePaths`); the store itself never interprets content.

## Amazon SP-API ingestion (scheduled pulls)

- **Schedules are data, managed in the app** (Tools → Amazon sync, Operator role): `ops.SyncSchedule` rows with interval (min 15 min) or daily-at-local-time + IANA time zone, lookback, auto-promote, enabled. Next-run math is `ScheduleCalculator` (DST-safe, unit-tested). Default schedules are seeded **disabled**. Deleting a schedule is a **soft delete** (`DeletedAt`, EF global query filter): run history and the `ops.IngestedReport` ledger must survive, so use `IgnoreQueryFilters()` only where deleted schedules are wanted (Deleted tab, run-history labels, seeding). **Pause all** is the single-row `ops.SyncSettings.IsPaused`; it stops scheduled slots only (Run now / backfills still run) and never flips schedules' own switches.
- **Worker**: `SyncSchedulerWorker` (hosted service) runs due schedules and "Run now" requests sequentially. Due slots are claimed with a compare-and-swap on `NextRunAt`, so multiple instances never double-run. History is `ops.SyncRun`; `ops.IngestedReport` guarantees an Amazon report is ingested once.
- **Flow**: `ReportIngestionService` → `IAmazonReportsGateway` (request → poll → download, or list for settlements) → `IRawFileStore.SaveAsync` → `StageRawFileAsync` → optional `PromoteAsync`. Same path as uploads from the blob onward.
- **All SP-API HTTP goes through `SpApiPipelineHandler`** (LWA token via `LwaTokenProvider`, per-operation token buckets in `SpApiRateLimiter`, retry/backoff on 429/5xx, one refresh on 401/403, structured logging without secrets). Never create another `HttpClient` that calls `sellingpartnerapi-*.amazon.com`. Report documents are downloaded with a separate client that never sends the access token (pre-signed S3 URLs).
- **Modes** (`SpApi:Mode`): `Disabled` (default), `Simulated` (Development: `SimulatedReportsGateway` generates realistic files so the pipeline works without credentials), `Live` (Azure). Credentials `SpApi:ClientId/ClientSecret/RefreshToken` come from user-secrets or Key Vault only.
- This is a web-only SP-API client. Do not reference the WPF `AERai.Seller.SpApiClient` — the two apps stay independent (CI enforces it).

## Database: one centralized DB, five schemas

| Schema | Role |
|---|---|
| `stg` | Raw imports. `stg.ImportBatch` + one table per source (`OrderLine`, `InventoryRow`, `SettlementLine`). All business columns are `nvarchar`; every row is kept with its `RawLine` and an `ErrorMessage` after promotion. |
| `core` | Curated, typed, constrained tables (`Marketplace`, `Product`, `ProductCost`, `Order`, `OrderItem`, `InventorySnapshot`, `Settlement`, `SettlementLine`). Written **only** by `core.usp_PromoteImportBatch` and the Products/COGS tool (`Marketplace` is seeded reference data). |
| `rpt` | SQL views over `core` (`vw_DailySalesBySku`, `vw_OrderSummary`, `vw_InventoryPosition`, `vw_SettlementSummary`) mapped as EF keyless entities. Pages read these. |
| `ops` | Ingestion schedules, run history, and the ingested-report ledger. |
| `auth` | ASP.NET Core Identity tables. |

Flow: **upload → `stg` (raw) → promote proc (TRY_CONVERT + MERGE, idempotent) → `core` → `rpt` views → pages.** Views and the proc are created in migrations with `migrationBuilder.Sql` and must have matching `Down` SQL. When changing a view/proc, add a new migration that `CREATE OR ALTER`s it — never edit an applied migration.

Locally SQL Server and Azurite run in Docker (`docker compose up -d`). SQL Server uses the named volume `sql2025data` (fixed name, shared with the equivalent `docker run` command), so data survives container rebuilds.

Integration tests (`tests/AERai.Web.Infrastructure.Tests`) run only when `AERAI_TEST_SQL` (server connection string, no database), `AERAI_TEST_BLOB` (e.g. `UseDevelopmentStorage=true`), and/or `AERAI_TEST_MAILPIT` (e.g. `http://localhost:8025/`) are set; each test class creates and drops its own database / container. Without `AERAI_TEST_BLOB`, SQL tests use an in-memory raw store.

## Auth

ASP.NET Core Identity (email = username), cookie auth, no self-registration. Roles: `Admin` (users + everything), `Operator` (imports, promotion, Amazon sync schedules, COGS edits), `Viewer` (read-only). Every page requires login by default; `Account/Login` is the only anonymous page. `/Tools` requires Operator, `/Admin` requires Admin.

## Marketplaces (US / CA / UK)

- **Reference data**: `core.Marketplace` (seeded: US `ATVPDKIKX0DER`, CA `A2EUQ1WTGCTBG2`, UK `A1F83G8C2ARO7P`) holds each marketplace's currency, time zone, `sales-channel` value, SP-API region, and `IsActive`. UK starts **inactive** because it is in the EU region and needs its own SP-API authorization; set `IsActive = 1` once EU credentials exist.
- **Every record has a marketplace**: `stg.ImportBatch`, `core.Order`, `core.InventorySnapshot`, `core.Settlement`, `ops.SyncSchedule`, and `ops.SyncRun` carry a non-null `MarketplaceId`. Promotion stamps the batch's marketplace on what it writes; uploads choose one on the Import page, schedules have one each. `core.Product` is a shared SKU catalog, but **cost of goods is per marketplace** in `core.ProductCost` (no row = not set).
- **Channel guard**: promotion rejects order lines whose `sales-channel` belongs to another marketplace (blank = the batch's own), so a mixed file can never leak orders into the wrong marketplace.
- **Inventory is never pooled**: each marketplace has its own fulfillment network, so snapshots and `rpt.vw_InventoryPosition` are keyed by (marketplace, SKU).
- **The switcher**: `_MarketplaceSwitch` in the top bar posts to `/Marketplace/Switch`, which remembers the choice per user/browser in the `AERai.Marketplace` cookie (`IMarketplacePreference`). Pages get their marketplace from `ICurrentMarketplace` (Application), which falls back to the first active marketplace if the cookie is missing, unknown, or inactive. Every marketplace-scoped query takes a `marketplaceId`; never add a page query that reads across marketplaces.

## Dashboard & UI conventions

- **Dashboard rules live in `DashboardService`** (Application, unit-tested); `IDashboardQueries` only returns rows (`rpt.vw_SalesLine`, inventory positions, settlements, sync health). Pages format, never compute.
- **Local business time**: days/hours use the selected marketplace's `TimeZoneId` (US `America/New_York`, CA `America/Toronto`, UK `Europe/London`), not UTC. Comparisons use the same elapsed time ("today so far" vs "yesterday until now").
- **Honest comparisons**: if synced Orders history (earliest successful run's `DataStart`) doesn't cover the whole comparison window, changes are suppressed (`ComparisonAvailable = false`) instead of showing misleading percentages.
- **One currency**: every page shows one marketplace, so money is always in that marketplace's currency (USD/CAD/GBP, formatted `$`/`CA$`/`£` by `DashboardFormat`). Amounts from different marketplaces are never summed.
- **Theme**: Light / Dark / System via `wwwroot/js/theme.js` (loaded in `<head>` to avoid a flash; choice in localStorage). Colors are CSS tokens in `site.css` defined per `[data-bs-theme]`; components must use tokens, never hard-coded colors. No inline styles or scripts (CSP) — charts are SVG with attribute geometry and token-colored classes.
- **Responsive**: tables go inside `.table-responsive`; long Amazon titles use `.cell-truncate` + `title` tooltip. The sidebar collapses behind a CSS-only Menu toggle under 768px.
- **Razor gotchas**: inside a C# block only the first element on a line is markup — put sibling elements on separate lines. After any Razor compile error, other pages may report bogus errors (e.g. on `<partial model=...>`) from stale source-generator state: run `dotnet build-server shutdown`, delete `src/AERai.Web.UI/obj`, rebuild.

### Passwords (change / forgot / reset)
- **Change password** (`/Account/ChangePassword`, the email link in the top bar) requires the current password; other sessions are signed out within a minute (`SecurityStampValidatorOptions.ValidationInterval = 1 min`), the current one stays signed in.
- **Forgot → email → reset**: `IPasswordResetService` (Application) owns the rules: the same confirmation whether or not the account exists, no link for admin-locked accounts, single-use Base64Url tokens that expire after `PasswordResetService.LinkLifetime` (1 hour). A successful reset also clears a failed-attempt lockout.
- **Reset-poisoning guard**: emailed links are built by `PublicUrl` from `App:PublicBaseUrl` (required outside Development), never from the request Host header.
- **Abuse limit**: forgot-password POSTs are rate-limited to 5 per 15 minutes per client IP (`RateLimits.PasswordReset`).
- **Email**: `IEmailSender` → `SmtpEmailSender` (MailKit). Locally Mailpit (`docker compose`, inbox at http://localhost:8025); in Azure any SMTP provider via `Email:*` with the password in Key Vault (`Email--Password`). Never log email bodies (they contain reset links). Empty `Email:Host` disables email with a warning.

## Secrets & configuration

- Never commit secrets. Local: `web/.env` (gitignored, copied from `.env.example`) for Docker, `dotnet user-secrets` for the UI project.
- Amazon credentials: `SpApi:ClientId`, `SpApi:ClientSecret`, `SpApi:RefreshToken` (used now) and `AmazonAds:ClientId`, `AmazonAds:ClientSecret`, `AmazonAds:RefreshToken`, `AmazonAds:ProfileId` (reserved for the upcoming Ads integration; not read by code yet). Locally set them (and optionally `ConnectionStrings:Sql`) with `scripts/set-local-secrets.sh` (hidden prompts → user-secrets); in Azure they are Key Vault secrets named with `--` (e.g. `AmazonAds--RefreshToken`).
- Connection string key: `ConnectionStrings:Sql`. Raw storage: `ConnectionStrings:RawStorage` (`UseDevelopmentStorage=true` for Azurite, set in `appsettings.Development.json`) or, in Azure, `RawStorage:ServiceUri` with managed identity (storage account has shared keys disabled). Dev admin seed: `Seed:AdminEmail` / `Seed:AdminPassword` (only seeded when both are set).
- Azure: Key Vault via managed identity (`KeyVault:Uri`), Azure SQL with Entra-only auth (managed identity, no SQL password).

## Commands

```bash
cp .env.example .env                      # then set MSSQL_SA_PASSWORD
docker compose up -d                       # SQL Server + Azurite
scripts/set-local-secrets.sh              # database connection (+ optional SP-API / Ads credentials) → user-secrets
dotnet build AERai.Web.slnx
dotnet test AERai.Web.slnx
dotnet run --project src/AERai.Web.UI     # Development auto-applies migrations + seeds roles/admin
dotnet ef migrations add <Name> --project src/AERai.Web.Infrastructure --startup-project src/AERai.Web.Infrastructure --output-dir Persistence/Migrations
```
