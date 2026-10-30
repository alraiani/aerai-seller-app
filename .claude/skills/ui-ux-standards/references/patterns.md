# UI patterns reference

Recipes for the screens this app builds again and again. The component facts (class names, site.js hooks) are in `CLAUDE.md` *Dashboard & UI conventions*; this file is how to put them together.

## A list page

```
Page title (+ breadcrumb when it's a deeper page)          [section tabs when the area has peers]
[KPI tiles — clickable filters, aria-current on the active one]
<div data-fragment="name">   ← _XxxBody partial, OnGetAsync ends with this.PageOrFragment("_XxxBody")
  card
    .card-toolbar: count ("54 SKUs in United States"), .search-box, filter selects (data-autosubmit-form),
                   .segmented views, then the page's own actions (soft buttons; primary last/right)
    bulk bar (data-bulk-bar) when rows can be ticked
    .table-responsive > table.data-table
       thead: column names (sortable: sort links with natural directions), totals row last
       tbody: rows; long text .cell-truncate + title; detail values .has-detail
    .empty-state when no rows: say why ("No orders match 'xyz'") + the way out (Clear filters)
    _Pager
</div>
```
- Filters, sorts, views, and paging are GET links/forms inside the fragment, so they reload only the list and keep the URL real.
- Never a `data-dirty-form` inside a fragment (a swap would discard edits) — editable lists reload whole.
- Server-side paging always; counts follow the filters.

## Forms

- Label every field (`<label asp-for>`), put format hints under it (`.form-text`), validation beside it (`<span asp-validation-for>`), and a summary at the top only when the form is long.
- One primary submit, named for the outcome ("Save schedule", "Upload and review").
- POST → redirect → GET with a `TempData` status message that says what happened and what's next ("Logged 12 units. Amazon should show them within a day — see Awaiting Amazon").
- Every POST form gets the busy behavior from site.js (submitter disabled, "Working…"); opt out with `data-busy="off"` only for instant client-side forms.
- Auth inputs carry `autocomplete` (`username`, `current-password`, `new-password`, `one-time-code`).

## Destructive and outward actions

- `data-confirm` with a sentence naming the object and the consequence: "Re-stage batch 42? This creates a new batch from the same file."
- Prefer reversible operations (ledger reversals, soft delete) and say how to undo in the success message.
- Re-doing something that already happened (re-export, repeat backfill) warns with when and who did it last.

## Upload → preview → apply

1. Upload page: what file, its layout (download a template), what happens next. Button "Upload and review".
2. Preview: counts (new / changed / unchanged / rejected), the changes table, rejected rows with reasons; nothing saved yet — say so.
3. Apply: one primary button, busy state, then PRG to the list with a status message. Stale rows are skipped and listed, never silently overwritten.

## Times, money, numbers

- Times: marketplace local zone (`ICurrentMarketplace` → `TimeZoneId`); relative ("2 h ago") with the exact local time as a `.has-detail` tip; name the zone in the toolbar or footnote with a friendly name ("Eastern time"), never a raw IANA id or "UTC" unless the data really is UTC-only (and then say so).
- Durations: "1 h 2 m", "45 s" — never raw seconds.
- Dates: "Oct 4 – Oct 18"; add the year when it is not the current one.
- Money: `SettlementFormat.Money` / `DashboardFormat.Money` (`$`, `CA$`, `£`, `MX$`), right-aligned in `.money` cells; negatives with a minus sign (and `amount-neg`); never "1234.50 USD".
- Counts: `N0`, proper plurals ("1 result", "2 results").

## Responsive toolbars

- `.card-toolbar` and its groups wrap; inputs go full width under 576px.
- Secondary actions may collapse into a "More" dropdown on narrow screens; the primary stays visible.
- Test at 375px: no page-level horizontal scroll.

## Tokens

- Colors only from tokens (light and dark values under `[data-bs-theme]`); add a token rather than a hex in a rule.
- Text on soft backgrounds uses the matching `--*-text` token (contrast).
- Type `--fs-*`, spacing `--space-*`, radius `--radius-*`, focus `--focus-ring`, elevation `--shadow`/`--shadow-lg`.
- No inline `style=""` or `<script>` (CSP); charts are SVG with token-colored classes.
