# Professional commenting standard

Comments are part of the API. They are written for the next engineer — who knows C# but not this business — and must stay true as the code changes.

## 1. XML documentation (`///`)

Required on every `public` (and `protected`) type and member in Domain, Application, and Infrastructure. Recommended on PageModel handlers.

| Tag | When |
|---|---|
| `<summary>` | Always. One or two sentences: what it *is* or what it *does* in business terms. Start with a verb for methods ("Promotes…"), a noun phrase for types ("A batch of rows…"). |
| `<param name>` | Every parameter whose meaning isn't obvious from its name and type. For `CancellationToken`, use the standard wording `Cancels the operation.` (the compiler requires it once any other `<param>` is documented). |
| `<returns>` | Every non-void method, describing what the value means (including failure cases for `Result`). |
| `<exception cref>` | Each exception the method deliberately throws. |
| `<remarks>` | Non-obvious behavior: idempotency, ordering, performance, thread-safety, why a design was chosen. |
| `<see cref>` / `<inheritdoc/>` | Cross-reference related types; use `<inheritdoc/>` on interface implementations instead of duplicating text. |

**Good**
```csharp
/// <summary>
/// Promotes every valid row in a staging batch into the curated <c>core</c> tables.
/// </summary>
/// <param name="batchId">Identifier of a batch in the <see cref="ImportBatchStatus.Received"/> or
/// <see cref="ImportBatchStatus.Promoted"/> state.</param>
/// <returns>A successful result with promoted/rejected row counts, or a failure when the batch does not exist.</returns>
/// <remarks>
/// Idempotent: re-promoting a batch upserts by natural key, so it never creates duplicates.
/// </remarks>
Task<Result<PromotionSummary>> PromoteAsync(long batchId, CancellationToken cancellationToken);
```

**Bad**
```csharp
/// <summary>PromoteAsync method.</summary>          // restates the name
/// <param name="batchId">The batch id.</param>      // adds nothing
```

## 2. Inline comments

Explain **why**, not **what**. If a comment describes what the next line does, rename things until it doesn't need to.

```csharp
// Good: explains a constraint the code can't express.
// Amazon reports purchase dates in UTC without an offset marker; treat them as UTC explicitly
// so promotion doesn't shift them to the SQL Server's local time zone.

// Bad: narrates the code.
// Loop through the rows
foreach (var row in rows)
```

Other rules:
- Full sentences, capitalized, ending with a period.
- `// TODO(owner): reason — https://link` — owner and reason required.
- `// NOTE:` for surprising-but-intentional behavior; `// HACK:` must include the removal condition.
- No commented-out code, no author/date headers, no change history.
- A null-forgiving `!` or a `#pragma warning disable` always gets a justification comment on the same or previous line.

## 3. Razor

`@* ... *@` comments only where markup structure is non-obvious (e.g., why a section is rendered conditionally). Don't comment every `<div>`.

## 4. SQL (views, procedures, migration SQL)

Every view and procedure starts with a header:

```sql
/*
  rpt.vw_DailySalesBySku
  Purpose : Units and revenue per SKU per purchase day, for the dashboard and sales reports.
  Grain   : One row per (SalesDate, Sku).
  Sources : core.[Order], core.OrderItem (cancelled orders excluded).
  Notes   : SalesDate is the UTC calendar date of PurchaseDate.
*/
```

Procedures add `Inputs`, `Side effects`, and `Idempotency` lines. Inline `--` comments explain business rules (e.g., why a status is excluded).

## 5. Bicep / YAML

- `@description('...')` on every parameter and output.
- A short header comment per module stating what it provisions and any manual steps (e.g., DNS records).
- Workflow steps get a `name:` that reads as a sentence; comment only non-obvious steps.
