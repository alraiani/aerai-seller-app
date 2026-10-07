# AERai Seller App

Internal Windows desktop app for Amazon seller operations: inventory management (stock visibility, demand forecasting, supplier→prep→FBA replenishment scheduling) and bookkeeping (SP-API order/fee/settlement sync, QuickBooks Desktop export). See `docs/plan.md` (mirrors the approved Claude Code plan) for the phased feature roadmap.

**Web app (`web/`)**: a separate ASP.NET Core Razor Pages + SQL Server + Azure app for seller.aeraigroup.com lives in `web/` with its own solution (`web/AERai.Web.slnx`) and its own rules in `web/CLAUDE.md`. The rules below are for the WPF app only; for anything under `web/`, follow `web/CLAUDE.md` and load the `web-coding-standards` skill.

## Solution layout & dependency direction

```
src/
  AERai.Seller.Domain/          # Entities, enums, value objects. No project references.
  AERai.Seller.SpApiClient/     # Independent SP-API client layer. No reference to Domain/EF Core/WPF.
  AERai.Seller.Application/     # Interfaces + business logic/services + DTOs. References Domain + SpApiClient.
  AERai.Seller.Infrastructure/  # EF Core (SQLite), IIF writer. Implements Application interfaces. References Application, SpApiClient.
  AERai.Seller.Presentation/    # ViewModels (CommunityToolkit.Mvvm). UI-framework-agnostic. References Application.
  AERai.Seller.Wpf/             # App.xaml, XAML Views, DI composition root only. References Presentation.
tests/
  AERai.Seller.Application.Tests/
  AERai.Seller.SpApiClient.Tests/
```

Dependencies only point downward: `Wpf → Presentation → Application → Domain`, with `SpApiClient` sitting beside `Domain` (referenced by `Application`/`Infrastructure`, never the reverse). A reference pointing the wrong way (e.g. `Domain` referencing `Infrastructure`, or `Presentation` referencing a WPF/WPF-UI type) is an architecture violation — this is exactly what the `architecture-reviewer` agent checks for.

### Domain namespace split: root vs `.Staging` vs `.Ai`

`AERai.Seller.Domain` is one project/assembly, but its types are split across three namespaces (mirrored by subfolders) to keep raw imports, curated master data, and AI-facing schema logically separate:

- **`AERai.Seller.Domain.Staging`** (`src/AERai.Seller.Domain/Staging/`): entities that are (near-)verbatim mirrors of data imported from SP-API — `Order`, `OrderItem`, `InventorySnapshot`/`InventoryState`, `CatalogItem`/`CatalogParent`, `SettlementReport`, `FinancialEvent`/`FinancialEventType`. When a new report/API sync is added (via `add-report-sync-job` or `add-sp-api-endpoint`), its entities belong here.
- **`AERai.Seller.Domain.Ai`** (`src/AERai.Seller.Domain/Ai/`): schema that supports AI-driven features — currently `LeadTimeProfile`, `DemandForecast`, `ReplenishmentRecommendation` for Phase 2 demand forecasting/replenishment. These are computed from staged data, not imports themselves.
- **Root `AERai.Seller.Domain`**: everything else — curated/master data that mixes manual input with denormalized references (`Product`, whose `CostOfGoods` is user-entered) and cross-cutting concerns that aren't business data (`SyncMetadata`, `AmazonBusinessDay`).

Files needing both a staged/AI type and a root type (e.g. `OrderRepository` uses `Order`/`OrderItem` from `.Staging` plus `AmazonBusinessDay` from root) simply add both `using` statements — this is a namespace-only split within one assembly, not a project boundary, so there's no new reference-direction rule to enforce here beyond the usual Domain-has-no-outgoing-references rule.

**Composition-root exception**: `AERai.Seller.Wpf` also has project references to `Infrastructure` and `SpApiClient`, in addition to `Presentation`. This is intentional and limited to one place: `App.xaml.cs` (the DI composition root), which must resolve concrete Infrastructure/SpApiClient implementations to register them against `Application` interfaces. No Infrastructure or SpApiClient type may be referenced from anywhere else in `Wpf` — not from a View, a code-behind event handler, or anywhere outside `App.xaml.cs`'s service registration. The `architecture-reviewer` agent checks the *usage*, not just the project reference, for this rule.

**Why layered this way**: the UI is WPF today but the plan explicitly keeps it swappable (Avalonia/Blazor/etc. later). `Wpf` must contain only Views and bootstrapping — zero business logic — so a future UI only requires a new front-end project referencing the same `Presentation`/`Application` layers.

## SP-API calls: always through the pipeline

Every SP-API HTTP call in the entire app must go through `AERai.Seller.SpApiClient`'s `SpApiRequestPipeline`. No project may construct its own `HttpClient` to call `sellingpartnerapi-*.amazon.com` directly — not Infrastructure, not anywhere else. This is the single choke point for:

- Attaching the LWA access token (via `LwaTokenProvider`, refreshed transparently before expiry or on 401).
- Per-operation rate limiting (token-bucket keyed by operation name — SP-API rate limits are per-operation, not global).
- Retry/backoff on 429/5xx with exponential backoff + jitter, honoring `x-amzn-RateLimit-Limit` when present; non-retryable 4xx errors surface immediately.
- Structured `ILogger` logging of every call (operation, status, retry count, latency) — never log tokens or secrets.

New API models are added as new typed clients (`OrdersApiClient`, `ReportsApiClient`, etc.) inside `SpApiClient` that call through the pipeline — see the `add-sp-api-endpoint` skill.

## Credentials

`client_id` / `client_secret` / `refresh_token` are never stored or logged in plain text. They're persisted encrypted at rest via Windows DPAPI (`System.Security.Cryptography.ProtectedData`), entered/edited through the Settings page, and exposed to `SpApiClient` only through the minimal `ICredentialStore` interface — `SpApiClient` has no knowledge of DPAPI or WPF, it just asks for credentials.

## Known namespace collisions

- **`Application`**: `AERai.Seller.Application` (our project) and `System.Windows.Application` (WPF's app class) share the `AERai.Seller` root from the `Wpf` project's point of view, so an unqualified `Application` in `Wpf` project C# code resolves to our namespace, not WPF's class, and fails to compile (`CS0118`). Always write `System.Windows.Application` fully qualified in the `Wpf` project (e.g. `class App : System.Windows.Application`, `System.Windows.Application.Current`) — this is not a bug to "fix" by renaming the Application layer, which is a standard Clean Architecture name.
- **`Wpf`**: the `AERai.Seller.Wpf` *project/assembly* name would put all its code in a C# namespace ending in `...Wpf`, which collides with the third-party `Wpf.Ui` namespace (WPF-UI) the same way — any unqualified `Wpf.Ui.*` reference inside a namespace ending in `Wpf` resolves to our own namespace segment first and fails (`CS0234`), and this hits XAML-generated code too, not just hand-written C#. Fixed structurally: the project's `<RootNamespace>` is set to `AERai.Seller.Desktop` in the `.csproj`, so all C#/XAML in this project uses `AERai.Seller.Desktop` (and `AERai.Seller.Desktop.Views`, etc.) even though the project/folder/assembly is still named `AERai.Seller.Wpf`. When adding new files here, match the existing `AERai.Seller.Desktop` namespace, not the project name.

## MVVM conventions

- ViewModels use `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`) and live in `AERai.Seller.Presentation`, never in the `Wpf` project.
- Code-behind (`.xaml.cs`) contains only view wiring (e.g. `InitializeComponent()`, control-specific event plumbing that can't be done via binding) — no business logic, no direct data access.
- Views bind to ViewModels via DI-resolved `DataContext`, not `new`'d up in code-behind.

## DI, hosting & logging

- `Microsoft.Extensions.Hosting` generic host is the composition root, wired up in the `Wpf` project's `App.xaml.cs`. All services are constructor-injected.
- Logging uses the native `Microsoft.Extensions.Logging` `ILogger<T>` abstraction everywhere — no third-party logging library (no Serilog), no `Console.WriteLine`/`Debug.WriteLine`. A small custom `AddProvider`-registered file-logger provider handles persistent on-disk logs alongside Debug/Console providers for local dev.
- Scheduled/background sync runs as an `IHostedService`.

## UI shell: theming & navigation

- [WPF-UI](https://github.com/lepoco/wpfui) (Fluent 2 / Windows 11 style) is used in the `Wpf` project only, for theming and navigation shell. `ApplicationThemeManager` drives Light/Dark/System switching; a `ThemeService` in `Presentation` wraps it so ViewModels never take a direct WPF-UI dependency.
- Main navigation is a left sidebar (`NavigationView`) with top-level sections: Dashboard, Inventory, Replenishment, Orders, Bookkeeping, Settings. Each section is a `Page`/ViewModel pair.

**Known gotcha — pages must not add their own page-level `ScrollViewer`.** `NavigationView`'s content host (`NavigationViewContentPresenter`, which extends `Frame`) wraps every navigated page in its own `DynamicScrollViewer` automatically (`IsDynamicScrollViewerEnabled` defaults to `true`) — that's what actually scrolls the page. An inner `ScrollViewer` added in a page's own XAML ends up nested inside that outer one, receives unbounded available height from it, and can never detect real content overflow (`ScrollableHeight` stays `0` no matter how much content there is — confirmed via `ScrollChanged`/`PreviewMouseWheel` diagnostic logging showing `ExtentHeight` always equal to `ViewportHeight`). Symptom: mouse wheel does nothing anywhere on the page. Fix: don't wrap page content in `ScrollViewer` — use a plain `StackPanel`/`Grid` as the page's root (with `Margin` for padding) and let the outer `DynamicScrollViewer` handle scrolling, as done on the Dashboard and Settings pages.

**Known gotcha — some other WPF-UI 4.3.0 control restyles are broken too.** Found by direct testing, not assumption — don't assume a WPF-UI control works correctly just because it renders; verify interaction (bindings, input) before relying on it.
- `CalendarDatePicker.Date` doesn't reliably two-way bind (its DP is registered with plain `PropertyMetadata`, not `FrameworkPropertyMetadata`) — use the standard `DatePicker` (`SelectedDate`) instead, as done on the Dashboard page.
- The `ControlsDictionary` `ScrollViewer` restyle never computes a non-zero `ScrollableHeight` — mouse wheel and the scrollbar appear to do nothing regardless of actual content overflow, confirmed via `PreviewMouseWheel` diagnostic logging (event reaches the ScrollViewer fine, `Handled=False`, but `ScrollableHeight` stays `0`). Fix: set `Style="{x:Null}"` on the affected `ScrollViewer` to fall back to WPF's default (correctly-behaving) template — see `DashboardPage.xaml`/`SettingsPage.xaml`. This only changes that scrollbar's visual style, not layout elsewhere.

## Database

SQLite via EF Core (single-machine app, no concurrent access needed). Migrations live in `Infrastructure`.

**Known gotcha — `DateTimeOffset` filtering on Sqlite**: the current `Microsoft.EntityFrameworkCore.Sqlite` provider cannot translate range comparisons (`>=`/`<`), component access (`.Year`/`.Month`/`.Day`), or `MaxAsync` over a `DateTimeOffset` property — each throws `InvalidOperationException: ... could not be translated` at query execution time, not at compile time. Work around it by projecting/materializing first (`.Select(o => o.SomeDateTimeOffsetProp).ToListAsync()` or `.ToListAsync()` on the whole set) and filtering/aggregating with LINQ-to-Objects afterward — see `OrderRepository` in Infrastructure for the pattern. `DateOnly` columns (e.g. `InventorySnapshot.SnapshotDate`) don't appear to hit this.

Repositories take `IDbContextFactory<SellerDbContext>` (registered via `AddDbContextFactory`, not `AddDbContext`) and create a short-lived `DbContext` per method call — Microsoft's recommended EF Core pattern for WPF/desktop apps. This means repositories, and everything that depends on them (Application services), can safely be registered as `Singleton` in DI without captive-dependency issues, avoiding an ASP.NET-style scope-per-operation ceremony that doesn't map naturally onto a desktop app.

## Postman

The "Amazon Workspace" Postman workspace has a collection scoped to exactly the SP-API operations this app calls (one folder per API model, mirroring `SpApiClient`'s typed clients). Whenever a new SP-API operation is added via `add-sp-api-endpoint`, add or update the matching Postman request in the same change — it's the tool for manually testing/visualizing real responses, in addition to (not instead of) `SpApiClient.Tests`.

## Available skills & agents

- `add-vertical-feature` — scaffold a new feature end-to-end across all layers.
- `add-sp-api-endpoint` — add a new typed SP-API client call (+ matching Postman request).
- `add-report-sync-job` — implement a new Reports-API-backed sync (request → poll → download → parse → upsert).
- `github-board-updates` — move items on the GitHub project board and log progress (what + why) as comments on the item's issue; use for every board item you work on.
- `architecture-reviewer` agent — run at the end of each phase/feature to check a change against the rules in this file before moving on.
