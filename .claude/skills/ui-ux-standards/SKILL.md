---
name: ui-ux-standards
description: Modern UI/UX principles, accessibility (WCAG 2.2 AA), and the house component patterns for the AERai seller web app's Razor Pages, site.css, and site.js. Load before building or changing any page, partial, style, or script, and when reviewing a page's usability or accessibility, so every screen looks, behaves, and reads the same way.
---

# AERai UI/UX standards

Applies to every page, partial, stylesheet, and script in `src/AERai.Web.UI`. Use it together with `web-coding-standards` (code) and the *Dashboard & UI conventions* in `CLAUDE.md` (the facts of each component). This skill says **how a screen should work for the person using it**.

Load the references when the task touches that area:
- `references/accessibility.md` — the WCAG 2.2 AA checks that apply here and how to verify each.
- `references/patterns.md` — recipes: a list page, forms, destructive actions, upload → preview → apply, times and money, responsive toolbars, tokens.

## 1. Principles

1. **One clear purpose and primary action per screen.** The title says what the page is; the most common next step is the one filled (`btn-primary`) button; secondary actions are soft buttons; rare or dangerous ones are last.
2. **Visual hierarchy follows importance.** Headline numbers in tiles, details in tables, explanations in tips. Group related things in one card; don't make people compare across cards.
3. **Consistency beats novelty.** Reuse the house components (§2). A list, a filter, a status, or a confirm looks and behaves the same everywhere; a new pattern needs a reason and a `CLAUDE.md` line.
4. **Every action gives feedback.** Pressing a button shows it is working (`data-busy`), ends in a status message (PRG + `TempData`), and a partial reload says it updated. Silence reads as broken.
5. **Forgiveness.** Destructive or outward actions confirm first (`data-confirm`, naming what happens: "Lock jane@…? They are signed out."). Prefer undo or reversal where the domain allows (ledger reversals). Unsaved edits are protected (`data-dirty-form`).
6. **Progressive disclosure.** Show what most people need; put the rest one step away: collapsible groups, views (`.segmented`), hover/click details (`.has-detail`), `_Tip` explanations. Never hide what's needed to act.
7. **Plain language.** Say what it means to the seller ("Waiting on Amazon", "Not paid out yet"), not how it is stored. Expand abbreviations once or in a tip. Sentence case. Buttons are verbs ("Upload and review", not "Submit").
8. **Honest data.** Money always with its currency (`SettlementFormat.Money`/`DashboardFormat.Money`), never summed across currencies; dates and times in the **marketplace's local zone** with the zone named when it matters; say when data is stale, partial, or missing ("No rate yet", "as of …") instead of showing a misleading zero.
9. **Accessible by default.** Keyboard reaches and sees everything, screen readers hear names and changes, nothing depends on color or hover alone, text and controls meet contrast. See `references/accessibility.md`.
10. **Works on a phone.** Every page is usable at 375px: toolbars wrap, tables scroll inside `.table-responsive`, no fixed widths that overflow.
11. **Progressive enhancement.** Pages work without script (links, forms, native `<details>`, `title`); site.js adds partial reloads, tooltips, sticky headers, busy states. CSP: no inline styles or scripts, ever.

## 2. House component map (use these, don't invent)

| Need | Use |
|---|---|
| Headline numbers / filters by status | `card tile` KPI tiles (links when they filter; `aria-current` on the active one) |
| Search | `.search-box` (every word must match: `ProductSearch.Terms`) |
| Filters | selects with `data-autosubmit-form` |
| Views of one list | `.segmented` switch |
| Peer pages of one area | `.section-tabs` (never toolbar link buttons) |
| List header and actions | `.card-toolbar` above a `.data-table` inside `.table-responsive` |
| Totals | last row of `<thead>` (`tr.totals-row`) |
| Status | `.pill` (`pill-ok/warn/bad/info`), always with text |
| Nothing to show | `.empty-state` that says why and offers the next step (e.g. *Clear filters*) |
| Explanation of a label | `_Tip` "?" |
| Detail behind a value | `class="has-detail"` + `title` |
| Many rows at once | `data-select-row` + `data-bulk-bar` |
| Dangerous action | `data-confirm="…"` on the form or button |
| Editable table | `data-dirty-form` (sticky save bar, warn on leave) |
| Long POST | `data-busy` behavior (every POST; opt out with `data-busy="off"`) |
| List that filters, sorts, pages | `data-fragment` section + `PageOrFragment("_XxxBody")` |
| Product in a row | `_ProductCell` |
| Paging | `_Pager` |
| Colors, spacing, type, radius | CSS tokens in `site.css` (`--surface`, `--muted`, `--info`, `--fs-*`, `--space-*`, `--radius-*`, `--focus-ring`) |

## 3. Pre-finish UX checklist

Before calling UI work done, check each:

1. The page's purpose and primary action are obvious; button labels are verbs.
2. It uses the components above; any new pattern is documented in `CLAUDE.md`.
3. Every POST gives busy feedback and ends with a status message; destructive ones confirm; editable tables protect unsaved edits.
4. Empty, zero, loading, stale, and error states are designed, not left blank.
5. Money has its currency; times are marketplace-local; units are named.
6. Keyboard only: Tab reaches every control in a sensible order with a visible focus ring; Escape closes popups; nothing traps focus.
7. Every input has a label, every icon-only button an `aria-label`, every status has text (not color alone), every image an `alt`.
8. Text ≥ 4.5:1 and UI parts ≥ 3:1 against their background in **light and dark**; colors come from tokens.
9. At 375px nothing overflows the page; toolbars wrap; targets are ≥ 24×24px.
10. Partial reloads keep focus and announce; it all still works without script.
11. Checked in the browser pane (light, dark, mobile) with no console errors.
