# AERai Seller App

Internal Windows desktop app for Amazon seller operations: inventory management (stock visibility, demand forecasting, supplier→prep→FBA replenishment scheduling) and bookkeeping (SP-API order/fee/settlement sync, QuickBooks Desktop export). See `docs/plan.md` (mirrors the approved Claude Code plan) for the phased feature roadmap.

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
- `architecture-reviewer` agent — run at the end of each phase/feature to check a change against the rules in this file before moving on.
