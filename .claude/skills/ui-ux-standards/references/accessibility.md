# Accessibility reference (WCAG 2.2 AA, as it applies here)

Each check names the success criterion, what it means on this app, and how to verify it in the browser pane.

## Perceivable

| Check | SC | Here | Verify |
|---|---|---|---|
| Text contrast ≥ 4.5:1 (≥ 3:1 for ≥ 18.66px bold / 24px) | 1.4.3 | Body, `--muted` labels, pill text on soft backgrounds, sidebar category labels, in **both themes** | `getComputedStyle` color + background → contrast ratio (formula below) |
| UI contrast ≥ 3:1 | 1.4.11 | Input and segmented borders (`--border-strong`), focus ring, chart bars, toggle outlines | same, against the adjacent color |
| Not color alone | 1.4.1 | Status pills have text; late/overdue/ACoS colors also say it in words or an icon with a label | read the page as text (`get_page_text`): is the meaning still there? |
| Text alternatives | 1.1.1 | Product images `alt` = title or SKU; decorative icons `aria-hidden`; charts have a text/table equivalent | `read_page` shows names |
| Info and relationships | 1.3.1 | `<th scope>` on headers (and on totals row labels), `<label for>` on inputs, `<fieldset><legend>` on radio groups, headings in order | `read_page` |
| Reflow at 320–375px | 1.4.10 | No page-level horizontal scroll; tables scroll inside `.table-responsive` | `resize_window` mobile, `document.documentElement.scrollWidth <= innerWidth` |
| Content on hover/focus | 1.4.13 | Tooltips dismissible (Escape), hoverable, persistent while focused (`.has-detail`) | Tab to it, press Escape |

## Operable

| Check | SC | Here | Verify |
|---|---|---|---|
| Keyboard | 2.1.1 | Every action reachable by Tab/Enter/Space; custom clickables are `<a>`/`<button>` or get `tabindex="0"` + key handling | Tab through the page |
| Bypass blocks | 2.4.1 | "Skip to content" link first in `_Layout` | first Tab shows it |
| Focus order | 2.4.3 | Follows reading order; after a partial reload focus returns to the same control or the region | Tab, then trigger a filter and Tab again |
| Focus visible | 2.4.7 | `:focus-visible` uses `--focus-ring`; never `outline: none` without a replacement | Tab; screenshot |
| Focus not obscured | 2.4.11 | Sticky header, toolbar, and save bar never fully cover the focused element (`scroll-padding`) | Tab down a long table |
| Target size ≥ 24×24 | 2.5.8 | Icon buttons, `.tip`, pill links, table checkboxes, rail toggles | `getBoundingClientRect()` |
| Link purpose | 2.4.4 | Link text (or `aria-label`) says where it goes; "View" → "View run history for Orders" | `read_page` |
| Motion | 2.3.3 (AAA, adopted) | Animations/transitions off under `prefers-reduced-motion: reduce` | emulate reduced motion |

## Understandable

| Check | SC | Here | Verify |
|---|---|---|---|
| Labels or instructions | 3.3.2 | Visible labels; format hints ("whole units", "yyyy-mm-dd") next to the input | read the form |
| Error identification | 3.3.1 | `asp-validation-for` beside each field, linked by `aria-describedby`; summary at top for many errors | submit invalid data |
| Error prevention | 3.3.4 | Preview → apply for uploads, confirm for destructive/financial actions | |
| Consistent navigation/identification | 3.2.3 / 3.2.4 | Same components, same names everywhere (see SKILL §2) | |
| Redundant entry / accessible auth | 3.3.7 / 3.3.8 | Password managers work: `autocomplete="username"`, `current-password`, `new-password`, `one-time-code` | inspect inputs |

## Robust

| Check | SC | Here | Verify |
|---|---|---|---|
| Name, role, value | 4.1.2 | Toggles use `aria-pressed`/`aria-expanded`; icon buttons have `aria-label`; disabled links are not focusable | `read_page` |
| Status messages | 4.1.3 | Partial reloads and async results are announced through the shared `aria-live` region; errors use `role="alert"` | screen reader or check the live region's text |

## Contrast formula (for `javascript_tool`)

```js
const L = c => { const [r,g,b] = c.match(/\d+(\.\d+)?/g).slice(0,3).map(v => { v/=255; return v<=0.03928 ? v/12.92 : ((v+0.055)/1.055)**2.4; }); return 0.2126*r+0.7152*g+0.0722*b; };
const ratio = (a,b) => { const [x,y] = [L(a),L(b)].sort((p,q)=>q-p); return (x+0.05)/(y+0.05); };
// ratio(getComputedStyle(el).color, getComputedStyle(bgEl).backgroundColor)
```
Semi-transparent backgrounds must be blended over the surface first.
