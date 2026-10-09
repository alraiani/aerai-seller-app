# Amazon Seller Ops App — Inventory & Bookkeeping Automation

## Context

You run an Amazon seller business and currently manage inventory and bookkeeping manually: downloading reports from Seller Central by hand and entering data into a desktop copy of QuickBooks. You already have SP-API credentials (client_id, client_secret, refresh_token) from an authorized app, and a Postman workspace ("Amazon Workspace") with a working Authorization request for testing.

The goal is an internal Windows desktop app that talks to the Selling Partner API (SP-API) directly, replacing the manual report-download/data-entry workflow for two areas:
1. **Inventory management** — know current stock levels by location/state, forecast demand, and know when/how much to reorder from suppliers and ship to FBA so you never stock out. Track COGS/profitability.
2. **Bookkeeping** — pull orders/fees/settlements automatically and get them into QuickBooks Desktop without manual re-entry.

Your supply chain has stages Amazon doesn't see: you order raw product from suppliers, prep/package each unit yourself, then bulk-ship to FBA. A core requirement is a replenishment/shipping schedule that accounts for the *entire* pipeline (supplier lead time → prep/packaging time → FBA transit/receiving time), not just Amazon's own inventory numbers, so you reliably know how many units to make and by when.

Decisions made with you:
- **UI**: WPF, but architected in layers so the UI can be swapped later (Avalonia, Blazor, etc.) without touching business logic.
- **Database**: SQLite + EF Core (single-machine app, no concurrent access needed).
- **QuickBooks**: MVP exports IIF/CSV files for manual import into QuickBooks Desktop (no COM/QBXML integration yet).
- **Users**: single user, single machine — no auth/roles needed.

## Architecture

Layered/Clean Architecture so the WPF shell is a thin, replaceable layer:

```
src/
  AERai.Seller.Domain/          # Entities, enums, value objects (incl. inventory states, lead times, replenishment plans). No dependencies.
  AERai.Seller.SpApiClient/     # Independent SP-API client layer (see below). No Domain/EF Core/WPF dependency — portable on its own.
  AERai.Seller.Application/     # Interfaces + business logic/services + DTOs. Depends on Domain + SpApiClient's public contracts.
  AERai.Seller.Infrastructure/  # EF Core (SQLite), IIF writer, adapts SpApiClient responses into Domain data. Implements Application interfaces.
  AERai.Seller.Presentation/    # ViewModels (CommunityToolkit.Mvvm), UI-framework-agnostic. Depends on Application.
  AERai.Seller.Wpf/             # App.xaml, XAML Views, DI composition root only. Depends on Presentation.
tests/
  AERai.Seller.Application.Tests/
  AERai.Seller.SpApiClient.Tests/
```

Key principle: `Wpf` project contains only Views + bootstrapping. All ViewModels live in `Presentation` so a future UI swap only requires a new front-end project referencing the same `Presentation`/`Application` layers.

**DI/Hosting**: `Microsoft.Extensions.Hosting` generic host inside the WPF app for DI, configuration, logging, and a background `IHostedService` for scheduled sync. Logging uses the native `Microsoft.Extensions.Logging` `ILogger<T>` abstraction throughout (no third-party logging library) — Debug/Console providers for local dev, plus a small custom lightweight file-logger provider (a few dozen lines, just appends structured lines to a rolling local file) registered via `AddProvider` for persistent on-disk logs.

**Credentials**: client_id/client_secret/refresh_token must never be stored in plain text. Store them encrypted at rest using Windows DPAPI (`System.Security.Cryptography.ProtectedData`) in a local config file. A Settings page (in the nav sidebar) is where you enter/update them; it writes through an `ICredentialStore` that `SpApiClient` consumes, so the client layer itself never touches DPAPI/WPF directly — it just asks for credentials via an interface.

## SP-API client layer (independent, shared by every API model)

This is the single choke point every SP-API call goes through — no code outside `AERai.Seller.SpApiClient` is allowed to call Amazon directly. It's a standalone class library (no reference to Domain, EF Core, or WPF) so it stays reusable regardless of which API model (Reports, Orders, Finances, FBA Inventory, Feeds, ...) is being called, and could even be reused outside this app later.

- **`ICredentialStore`**: minimal interface (`GetCredentials()` → client_id/client_secret/refresh_token/marketplace/region) that the host app (Wpf/Infrastructure) implements against the DPAPI-encrypted settings store. Supplied to `SpApiClient` via DI — the client layer has zero knowledge of *how* credentials are stored, only that it can fetch them.
- **`LwaTokenProvider`**: exchanges the refresh_token for a short-lived access_token (~1hr) via LWA, caches it in memory, and transparently refreshes it before expiry or on a 401. SP-API no longer requires AWS SigV4/IAM signing (dropped 2023) — just the `x-amz-access-token` header.
- **`SpApiRequestPipeline`**: the single method every typed call routes through. Responsible for:
  - Attaching the access token.
  - **Per-operation rate limiting**: a token-bucket limiter keyed by operation name (each SP-API operation publishes its own rate/burst limits — they are not global), so one hot operation can't starve others.
  - **Throttling/retry handling**: on 429 (or 5xx), retry with exponential backoff + jitter up to a bounded max, honoring the `x-amzn-RateLimit-Limit` response header when present; non-retryable errors (4xx other than 429/401) surface immediately.
  - Structured `ILogger` logging of every call (operation, status, retry count, latency) without ever logging tokens/secrets.
- **Typed per-API-model clients** (`OrdersApiClient`, `ReportsApiClient`, `FinancesApiClient`, `FbaInventoryApiClient`, `FeedsApiClient`, ...): thin, generated-or-hand-written wrappers over `SpApiRequestPipeline`. Adding support for a new SP-API model means adding a new typed client here — it cannot bypass the pipeline. This is exactly the piece the Phase 0 `add-sp-api-endpoint` skill scaffolds.

## UI shell: theming & navigation

- **Theming**: adopt the [WPF-UI](https://github.com/lepoco/wpfui) toolkit (Fluent 2/Windows 11 style controls) in the `Wpf` project only — keeps Application/Domain/Presentation framework-agnostic. Use its `ApplicationThemeManager` for Light/Dark (and System) theme switching applied at runtime via merged ResourceDictionaries, plus its accent-color support. Add a theme-switcher control (e.g. a toggle in the nav pane footer or a Settings option) bound to a `ThemeService` in `Presentation` that wraps the WPF-UI theme manager so ViewModels don't take a direct WPF-UI dependency.
- **Navigation**: left sidebar nav pane using WPF-UI's `NavigationView`, with top-level sections: Dashboard, Inventory, Replenishment, Orders, Bookkeeping, Settings. Each section maps to a `Page`/ViewModel pair; nested sub-pages (e.g. Inventory > Stock Levels / COGS) can be added as sub-items later without changing the shell.
- Persist the user's last-selected theme (and any nav state worth remembering) in the same local settings store as SP-API credentials.

## SP-API models covered (via the typed clients above)

Reference the official models at github.com/amzn/selling-partner-api-models (Reports, Orders, Finances, FBA Inventory, Feeds APIs) — import into the existing Postman workspace to explore sample responses before coding each typed client.

## Postman collection (test/visualize responses, kept in sync with the app)

In the existing "Amazon Workspace", create one well-structured collection scoped to exactly what this app calls — not the full SP-API surface. Structure:

- One folder per typed client / API model (Reports, Orders, Finances, FBA Inventory, Feeds, ...), mirroring `AERai.Seller.SpApiClient`'s layout 1:1.
- Reuses the existing Authorization request/refresh flow already set up in the workspace (chained via collection/environment variables so requests in the other folders pick up a fresh access token) rather than duplicating auth logic.
- Only the specific operations the app actually calls go in — e.g. under Reports: create-report / get-report / get-report-document for the specific report types listed above, not every report type SP-API offers.

**Ongoing convention**: every time a new SP-API operation is added to `AERai.Seller.SpApiClient` (via the `add-sp-api-endpoint` skill), add or update the matching request in this collection in the same change — Postman stays a living, accurate way to manually test and inspect real responses for everything the app uses. This is in addition to, not a replacement for, the `SpApiClient.Tests` unit tests.

- **Reports API**: request report → poll status → download → parse TSV. Used for inventory snapshots (`GET_FBA_MYI_ALL_INVENTORY_DATA`, `GET_LEDGER_SUMMARY_VIEW_DATA`, `GET_FBA_INVENTORY_PLANNING_DATA` for per-state quantities and Amazon's own restock signal) and settlements (`GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2` — the long amount-type/amount layout; the non-_V2 report is a wide layout the parser does not read).
- **Orders API**: order + order-item detail, incremental sync by `LastUpdatedAfter` — primary input for sales velocity/demand forecasting.
- **Finances API**: granular fee/refund/reimbursement events for reconciliation and QuickBooks categorization.
- **FBA Inventory API / Inventory Ledger**: per-SKU quantities broken out by state — Available, Inbound, Reserved, Unfulfillable, Researching, FC Transfer, FC Processing. This is Amazon's side of the pipeline only; it doesn't know about your supplier/prep stages, which are tracked internally (see Phase 2).
- **Feeds API**: (Phase 2+) push quantity/price updates back to Amazon — flagged since it's a live-listing side effect.

## Phased feature plan

**Phase 0 — Project conventions as Claude skills & agents (before any code)**

Codify the architecture rules below as reusable Claude Code skills/agents in this repo so every later phase is built consistently, instead of relying on memory/discipline alone:

- `.claude/CLAUDE.md`: written reference for the layering rules (dependency direction Domain/SpApiClient ← Application ← Infrastructure/Presentation ← Wpf), the rule that **all** SP-API calls must go through `SpApiRequestPipeline` (no direct `HttpClient` calls to Amazon anywhere else), MVVM conventions (CommunityToolkit.Mvvm, no logic in code-behind beyond view wiring), DI/logging conventions (constructor injection, `ILogger<T>` everywhere, no `Console.WriteLine`/`Debug.WriteLine`), credential-handling rules (DPAPI-encrypted via `ICredentialStore`, never logged), and the WPF-UI/NavigationView shell conventions from above.
- Skill `add-vertical-feature`: scaffolds a new feature end-to-end across all layers in the correct order (Domain entity → Application interface/service → Infrastructure implementation → Presentation ViewModel → Wpf View/Page), wiring DI registration and enforcing that each layer only references what the architecture allows.
- Skill `add-sp-api-endpoint`: conventions for adding a new typed client to `AERai.Seller.SpApiClient` for an SP-API model not yet covered — always routes through the shared `SpApiRequestPipeline` (never a standalone `HttpClient`), maps responses to plain DTOs, and points at the Postman "Amazon Workspace" collection + the official `selling-partner-api-models` GitHub repo for the expected request/response shape before coding against it. Also adds/updates the matching request in the Postman collection (see below) as part of the same change, so it never drifts out of sync with the app.
- Skill `add-report-sync-job`: conventions for the Reports API request → poll → download → parse → idempotent-upsert pattern used by every report-backed sync (inventory, orders, settlements, ledger).
- Agent `architecture-reviewer`: a repo-scoped review subagent that checks a change against the rules above (no business logic in `Wpf`, no outward dependencies from `Domain`, no SP-API call bypassing `SpApiRequestPipeline`, ViewModels don't take WPF-UI types directly, secrets never logged, tests exist for new Application/SpApiClient-layer logic). Run it as a gate at the end of every phase below before moving on.

Only after Phase 0's skills/agents/CLAUDE.md exist do we start writing Domain/Application/Infrastructure/Wpf code — every phase from here on is implemented *via* the Phase 0 skills, not ad hoc.

**Phase 1 — Foundation & inventory visibility**
- Solution scaffold per layered structure above; EF Core + SQLite migrations for core entities (Product/SkuMapping, InventorySnapshot — modeled per-SKU/per-state from the start so Phase 2 doesn't require a schema rework —, Order, OrderItem, FinancialEvent, SettlementReport).
- Settings screen: enter/store encrypted credentials (client_id/client_secret/refresh_token, marketplace/region) via `ICredentialStore`.
- Postman: scaffold the collection structure (folders per API model) in "Amazon Workspace" and use it to explore/verify the real request/response shape of each operation before coding it.
- Build the `AERai.Seller.SpApiClient` layer: `LwaTokenProvider`, `SpApiRequestPipeline` (per-operation rate limiting + retry/backoff), and the first typed clients (Reports, Orders) — each backed by a saved Postman request.
- Reports API sync: inventory + orders, manual "Sync Now" button.
- App shell: WPF-UI `NavigationView` sidebar (Dashboard, Inventory, Replenishment, Orders, Bookkeeping, Settings) with Light/Dark theme switching wired up from the start, since every later page hangs off this shell.
- WPF Dashboard: first/default page shown on launch. Its top widget is **today's orders summary grouped by SKU** (units sold + revenue per SKU, for today, from already-synced Orders data), plus last-sync status. Built as a widget-based layout so later phases add more tiles (replenishment alerts in Phase 2, financial summary in Phase 3) without restructuring the page.
- Inventory grid (current stock, basic low-stock flag).

**Phase 2 — Inventory depth: state tracking, demand forecasting & replenishment planning**
- COGS entry per SKU (manual input UI).
- **Inventory-by-state tracking**: store each sync as a per-SKU, per-state snapshot — Amazon states (Available, Inbound, Reserved, Unfulfillable, Researching, FC Transfer, FC Processing) from the FBA Inventory Ledger/Planning reports, *plus* internal pipeline states you track yourself since Amazon has no visibility into them: `OnOrderFromSupplier`, `InPrep`/`Packaging`, `ReadyToShipToFBA`. Manual UI (or simple checklist workflow) to move units between these internal states as raw product arrives, gets prepped, and gets bulk-shipped.
- **Demand forecasting**: compute rolling sales velocity per SKU (recent-weighted daily/weekly average from Orders API history, with a simple seasonality/trend adjustment), then project **days-of-supply** and an estimated **stockout date** from total sell-through-eligible stock (Available + Inbound + FC Transfer + FC Processing).
- **Lead time profile per SKU**: configurable supplier lead time (order → raw product arrival), prep/packaging time (raw product → ready to ship), and FBA inbound transit + receiving time (ship → available for sale), each with a safety-stock/buffer setting.
- **Replenishment/shipping schedule**: given forecasted demand + lead times + current pipeline quantities, compute per SKU: how many units to order from the supplier and by what date, how many are due out of prep, and how many to bulk-ship to FBA and by what date — so you always know how much to make and when, working backward from the projected stockout date. Surface as a dashboard sorted by urgency (days until action needed), cross-checked against Amazon's own restock recommendation report as a secondary signal.
- Low-stock/at-risk alerts (UI badge, optional Windows toast) when a SKU's replenishment action is overdue or the stockout date is inside the combined lead time; surfaced as a new Dashboard widget alongside the Phase 1 today's-orders-by-SKU widget.
- FBA inbound shipment status tracking.
- Optional: Feeds API push for price/quantity updates (explicit action, confirmed in-app before sending).

**Phase 3 — Bookkeeping depth**
- Finances API sync: fees, refunds, reimbursements categorized by type.
- Settlement reconciliation view (settlement deposits vs. orders/fees).
- IIF file generator mapping settlement periods into QuickBooks Desktop transactions (COGS, Amazon fees, shipping, ads, refunds, sales tax) — ready for File > Import.
- P&L by SKU/ASIN (revenue − COGS − fees − ads).
- Scheduled background sync (daily) via the hosted service.
- Dashboard gets a financial summary widget (recent settlement status, pending fees).

**Phase 4 — Polish**
- Error/log surfacing in-app.
- Unit tests for sync + IIF export logic (Application layer, UI-independent by design).
- Confirm UI-swap story: verify Presentation layer has zero WPF references.

## Verification

- End of each phase (1-4): run the `architecture-reviewer` agent from Phase 0 against the changes before moving to the next phase.
- Each phase: run the WPF app locally, trigger sync, confirm data lands correctly in the SQLite DB (inspect via a SQLite browser) and renders in the UI.
- Validate SP-API calls against sample responses gathered in the Postman "Amazon Workspace" before wiring up each client, to confirm expected shape/edge cases.
- For IIF export (Phase 3): generate a file from real settlement data and test-import it into a QuickBooks Desktop sample/test company file before using it on the real company file.
- Unit tests in `AERai.Seller.Application.Tests` for report parsing, reorder/replenishment calculations, demand forecasting, and IIF generation logic (pure functions, no live API calls needed).
- Unit tests in `AERai.Seller.SpApiClient.Tests` for the rate limiter and retry/backoff logic (simulate 429s/5xx via a fake `HttpMessageHandler`) — this is the piece every feature depends on, so it needs its own coverage independent of any specific API model.
- For the replenishment schedule specifically: validate its output against a few real SKUs by hand (known sales rate, known lead times) to confirm recommended order dates/quantities and stockout projections match expectations before trusting it operationally.
