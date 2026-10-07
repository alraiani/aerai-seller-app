# Patterns & conventions reference

## Layer responsibilities

| Project | Contains | May reference |
|---|---|---|
| `AERai.Web.Domain` | Entities (`Staging/`, `Core/`), reporting read models (`Reporting/`), enums. Plain C#. | nothing |
| `AERai.Web.Application` | Interfaces (`Abstractions/`), use-case services, DTO records, `Result`, parsing/validation logic, `AddApplication()`. | Domain, `Microsoft.Extensions.*.Abstractions` |
| `AERai.Web.Infrastructure` | `AppDbContext`, EF configurations, migrations, query/repository implementations, Identity adapters, blob raw store, `AddInfrastructure()`. | Application, EF Core, ASP.NET Core Identity, Azure SDKs |
| `AERai.Web.UI` | `Program.cs` (composition root), Razor Pages, layout, static assets. | Application; Infrastructure **only** in `Program.cs` |

A PageModel that needs data asks an Application interface. If no interface fits, add one to `Application/Abstractions` and implement it in Infrastructure — do not reach for `AppDbContext`.

## Data flow: source → blob → staging → core → reporting

```
source (Tools/Import upload today; SP-API report download later)
   │  StagingImportService.ImportAsync → IRawFileStore.SaveAsync (write-once, SHA-256)
   ▼
blob raw/{source}/{yyyy}/{MM}/{dd}/{id}-{file}   (landing zone, untouched bytes)
   │  StagingImportService.StageRawFileAsync parses CSV/TSV FROM the blob → string columns only
   ▼
stg.ImportBatch + stg.Stg*  (raw, nvarchar, every row kept, RawLine preserved)
   │  core.usp_PromoteImportBatch @BatchId  (TRY_CONVERT + MERGE, one transaction)
   ▼
core.*  (typed, constrained, natural-key unique)
   │  SQL views
   ▼
rpt.vw_*  (read models; EF keyless entities via ToView)  →  Razor Pages
```

Rules:
- Every file is landed in blob storage before parsing; staging always reads from the blob. New ingestion paths (e.g. an SP-API worker) save to the store and call `StageRawFileAsync` — they never write `stg` directly.
- Raw blobs are write-once and never deleted by the app; reprocessing uses `RestageAsync`, which creates a new batch.
- Staging tables never have FKs, CHECKs, or typed business columns — the point is that *anything* lands so it can be inspected.
- Promotion is idempotent: re-running a batch updates, never duplicates.
- Rows that fail conversion get `ErrorMessage` set and are skipped; the batch is `Promoted` with an error count, or `Failed` if the transaction rolled back.
- Pages never read `stg.*` except the Batches tool (to show errors) and never write `core.*` directly.
- A new report source = new `stg` table + promotion branch + (optionally) a new `rpt` view, all in one migration.

## Result pattern

```csharp
var result = await _importService.ImportAsync(command, ct);
if (result.IsFailure)
{
    ModelState.AddModelError(string.Empty, result.Error);
    return Page();
}
TempData["Status"] = $"Imported {result.Value.RowCount} rows.";
return RedirectToPage("/Tools/Batches");   // PRG
```

Use `Result` for outcomes the user can fix. Throw for programmer errors and infrastructure failure; the global exception handler logs and shows `/Error`.

## Options pattern

```csharp
services.AddOptions<ImportOptions>()
    .BindConfiguration(ImportOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Each options class has a `public const string SectionName` and data-annotation constraints.

## EF Core conventions

- Schemas: `stg`, `core`, `rpt`, `auth` — constants in `Schemas` class, never string literals scattered around.
- Keys: surrogate `long Id` identity for staging rows; natural keys get a unique index in core.
- Money: `decimal(18,2)`. Timestamps: `datetimeoffset`. Dates: `date` (`DateOnly`).
- Reporting views: `builder.ToView("vw_X", Schemas.Reporting).HasNoKey();`
- Queries: `AsNoTracking()`, project to DTO with `Select`, page with `Skip/Take` after a deterministic `OrderBy`.
- Migrations are applied automatically only in Development; production uses a migrations bundle from CI.

## Razor Pages conventions

- Folder = section (`Pages/Inventory`, `Pages/Tools`, `Pages/Admin`); authorization configured by folder in `Program.cs`.
- Input models are nested `sealed class InputModel` with data annotations; `[BindProperty] public InputModel Input { get; set; } = new();`.
- Status messages via `TempData["Status"]`, rendered by `_StatusMessage` partial.
- Paging via the shared `_Pager` partial and `PageRequest`/`PagedResult<T>` from Application.

## Security baseline

- Cookie auth: `HttpOnly`, `SecurePolicy=Always`, `SameSite=Lax`, sliding expiration, 8h lifetime.
- Identity: lockout after 5 failures for 15 minutes, password length ≥ 12, unique email, no self-registration (Admins create users).
- Uploads: max size from `ImportOptions`, extension allow-list (`.csv`, `.tsv`, `.txt`), parsed as text, never saved to disk with the user-supplied name.
- Production: HSTS, HTTPS redirect, forwarded headers for App Service, Key Vault and Blob Storage via managed identity (storage shared keys disabled), Azure SQL with Entra-only auth.
