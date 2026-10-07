---
name: web-coding-standards
description: Coding principles, patterns, practices, and professional commenting standards for the AERai web app under web/ (ASP.NET Core Razor Pages, EF Core, SQL Server, Azure). Use whenever writing, modifying, or reviewing any C#, Razor, SQL, Bicep, or pipeline code under web/ — new pages, services, entities, migrations, views, stored procedures, or infrastructure — so every change follows the same standards.
---

# AERai web coding standards

Applies to everything under `web/`. Read `web/CLAUDE.md` first for the architecture rules this skill enforces. The WPF app under `src/` is a domain reference only — never add a project reference to it and never copy its SQLite-specific workarounds.

Detailed guidance lives in two reference files — load them when the task touches that area:
- `references/patterns.md` — layer responsibilities, data flow (stg → core → rpt), Result/Options/PRG patterns, EF Core and SQL conventions, Razor Pages conventions, security.
- `references/commenting.md` — XML doc comments, inline-comment rules, SQL and Bicep headers, good-vs-bad examples.

## 1. Principles (non-negotiable)

- **SOLID.** One reason to change per class. Depend on abstractions defined in `Application`; inject implementations. Prefer small, role-specific interfaces (`IReportingQueries`, `IPromotionService`) over a god "`IDataService`".
- **Separation of concerns.** Business decisions live in `Application`. `UI` binds and renders. `Infrastructure` talks to SQL Server, Identity, and Azure. `Domain` is plain C#.
- **KISS / YAGNI.** Build the simplest thing that meets today's requirement. No speculative abstraction layers, generic repositories, or plugin systems.
- **DRY, but not prematurely.** Extract on the third repetition, or immediately when the duplicated thing is a business rule.
- **Fail fast.** Guard clauses at public boundaries (`ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`). Validate options at startup (`ValidateOnStart`).
- **Explicit over clever.** Readable code beats terse code. Name things for what they mean in the business ("ImportBatch", "PromoteAsync"), not for how they are implemented.

## 2. Patterns to use

| Concern | Pattern |
|---|---|
| Layering | Clean Architecture: `UI → Application → Domain`, `Infrastructure → Application`. UI references Infrastructure only in `Program.cs` (`AddInfrastructure`). |
| Data ingestion | Source → Blob → Staging → Core → Reporting: files land write-once in the raw blob container, are parsed from there into `stg.*` as raw text, a set-based promotion proc types/merges them into `core.*`, pages read `rpt.vw_*` views. |
| External APIs | One choke point per API: SP-API calls go only through `SpApiPipelineHandler` (auth, per-operation rate limits, retry/backoff, logging). Typed clients tag each request with its operation name. |
| Background work | `BackgroundService` + a DI scope per unit of work; claim shared work atomically (compare-and-swap) so scale-out is safe; record every run's outcome instead of throwing. |
| Reads | Query interfaces returning DTO `record`s, `AsNoTracking()`, server-side paging. |
| Writes | Application service method per use case (`ImportAsync`, `PromoteAsync`, `CreateUserAsync`). |
| Expected failures | `Result` / `Result<T>` (validation, "not found", bad credentials). Exceptions only for the truly exceptional (DB down, bug). |
| Configuration | Options pattern: `IOptions<T>` bound from a named section, `ValidateDataAnnotations().ValidateOnStart()`. |
| Forms | Post/Redirect/Get with `TempData` status messages; `[BindProperty]` only on the input model. |
| Time | Inject `IClock` (or `TimeProvider`) — never `DateTime.Now` in business logic. |
| DI lifetimes | `DbContext` and services that use it: Scoped. Stateless helpers/parsers: Singleton. |

## 3. Practices

- **C# style**: file-scoped namespaces, one public type per file, file name = type name, `sealed` by default for classes not designed for inheritance, `record` for DTOs/commands, `required` + `init` for entity/DTO properties that must be set, `var` when the type is obvious.
- **Naming**: PascalCase types/members, `_camelCase` private fields, `I` prefix on interfaces, `Async` suffix on async methods, SQL objects `schema.PascalCase` (`core.Order`), views `rpt.vw_Name`, procs `schema.usp_Name`.
- **Async**: async all the way; every async method accepts a `CancellationToken` and passes it on; never `.Result`/`.Wait()`.
- **Nullability**: NRT is on and warnings are errors — fix the nullability, don't `!` it away. A `!` needs a comment explaining why it is safe.
- **EF Core**: configuration in one `IEntityTypeConfiguration<T>` per entity under `Persistence/Configurations`; explicit schema, column types (`decimal(18,2)`, `nvarchar(n)`), and indexes. Migrations named for intent (`AddSettlementSummaryView`, not `Update3`). Raw SQL for views/procs goes in migrations via `migrationBuilder.Sql`, with matching `Down`. Use `ExecuteSqlInterpolatedAsync` (parameterized) — never string-concatenated SQL.
- **SQL**: set-based, transactional, idempotent (`MERGE`/upsert keyed by natural key). `TRY_CONVERT` in promotion so one bad row never fails a batch. Every view states its grain.
- **Razor Pages**: thin `PageModel`s — bind, call one Application service, map to view. No `DbContext`, `UserManager`, or `SignInManager` in a PageModel. Tag helpers over raw HTML for forms/links. Every list page is paged.
- **Security**: authorize by default (`AuthorizeFolder("/")`), opt out explicitly (`AllowAnonymousToPage`). Role policies for Tools/Admin. Antiforgery stays on. Secrets come from user-secrets (dev), `.env` (compose), or Key Vault (Azure) — never `appsettings.json`, never source, never logs. Validate and size-limit uploads.
- **Logging**: `ILogger<T>` with message templates (`"Promoted batch {BatchId} ({RowCount} rows)"`), never string interpolation in log calls, never log secrets/PII beyond user id/email where needed for audit.
- **Testing**: every Application service has xUnit tests (Arrange/Act/Assert, one behavior per test, `MethodName_Condition_ExpectedResult` naming). Infrastructure tests that need SQL Server skip cleanly when `AERAI_TEST_SQL` is not set.
- **Infrastructure as code**: Bicep modules per resource type, parameters with `@description`, no secrets in params files, managed identity over keys/passwords.

## 4. Professional commenting (summary — see references/commenting.md)

- `///` XML docs on **every public type and member** in Domain, Application, and Infrastructure: `<summary>` always; `<param>`, `<returns>`, `<exception>` where applicable; `<remarks>` for non-obvious behavior.
- Inline comments explain **why** (business rule, constraint, workaround), never restate **what** the code does.
- `// TODO(name): reason — link` format; no anonymous TODOs.
- SQL views/procs start with a header block: purpose, grain, inputs, side effects.
- No commented-out code, no banner art, no change-log comments (that's git's job).

## 5. Pre-finish checklist

Before calling a change under `web/` done, verify each:

1. `dotnet build web/AERai.Web.slnx` passes with zero warnings (warnings are errors).
2. `dotnet test web/AERai.Web.slnx` passes; new Application logic has tests.
3. No new reference violates the layering (UI ↛ Infrastructure outside `Program.cs`; Domain references nothing).
4. New pages are covered by authorization (default policy or explicit role policy); forms use antiforgery and PRG.
5. Schema changes have a named migration with a working `Down`; views/procs have headers.
6. All public members have XML docs; comments explain why.
7. No secrets, connection strings, or passwords in committed files (check `git diff` for `Password=`, `MSSQL_SA_PASSWORD=` with a real value, keys).
8. No new `HttpClient` calls Amazon outside `SpApiPipelineHandler`; no credentials in logs, exceptions, or UI.
9. `web/CLAUDE.md` updated if a convention changed.
