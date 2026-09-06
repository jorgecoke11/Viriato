# Casos list filter bar column count matches its control count

Written against: ba2cac26f146559bbbfa446162655567f6fbb62b

## Evidence chain

- Surface: `/casos/lista` route → `CasosListPage`, filter bar directly above the results table
- Problem: The filter bar declares `sm:grid-cols-3` for exactly two controls (the "Buscar" input, the "Estado" select), leaving a visibly empty third grid column at `sm` width and above.
- Design evidence: the sibling filter bar in the same surface, `frontend/src/features/casos/pages/DashboardPage.tsx:60` (Desde/Hasta), also has exactly two controls and uses `grid grid-cols-1 gap-4 sm:grid-cols-2 sm:max-w-md` — its column count matches its control count. Confirmed rendered at 800px width: the Dashboard's two-control row sizes cleanly to its content; the Casos-list two-control row leaves an empty trailing column.
- Owner: `frontend/src/features/casos/pages/CasosListPage.tsx:56`
- Scope and affected surfaces: `/casos/lista` only
- Uncertainty: none

## Design decision

Change the filter-bar grid on `/casos/lista` from `sm:grid-cols-3` to `sm:grid-cols-2` so the declared column count matches its two controls, consistent with the Dashboard filter bar in the same surface.

## Reuse

- `grid grid-cols-1 gap-4 sm:grid-cols-2` (existing Tailwind grid pattern)
- Exemplar: `frontend/src/features/casos/pages/DashboardPage.tsx:60`

No new primitive is required.

## Changes

1. `frontend/src/features/casos/pages/CasosListPage.tsx:56`
   - Change: replace `className="grid grid-cols-1 gap-4 sm:grid-cols-3"` with `className="grid grid-cols-1 gap-4 sm:grid-cols-2"` on the filter-bar wrapping `<div>` (containing the "Buscar" `Input` and the "Estado" `<select>`).
   - Preserve: `grid-cols-1` for mobile, the `gap-4` spacing, and both controls' own markup/classes.
   - Verify: rendered at `/casos/lista` on desktop width, the two controls fill the row evenly with no empty trailing column.
   - Note: do not add `sm:max-w-md` (present on the Dashboard exemplar) unless separately requested — that is an additional width-constraint decision not established as a contradiction by this audit. Changing only the column count is the minimal correction the evidence supports.

## Scope

- Inherit: only the filter bar on `/casos/lista`.
- Verify: no other page reuses this exact filter-bar markup. `TrabajosPage` (`sm:grid-cols-3` for three controls) and `CompaniesPage` (`sm:grid-cols-4` for four controls) already match their own control counts and are separate surfaces, not audited here — do not change them under this plan.
- Exclude: the results table, pagination controls, and the "Filtrando por flujo" banner above the filter bar — unrelated to the grid column count.

## Validation

- Product: open `/casos/lista` and confirm the "Buscar" input and "Estado" select occupy the full row width evenly with no dead space.
- Interface: check at mobile width (375px, single column — unaffected by this change) and desktop width (≥640px, the corrected two-column row).
- System: confirm the correction only touches column count, matching the Dashboard's established two-control grid pattern, without introducing a new width-constrained variant.
- Repository: `npx tsc -b --noEmit && npm run lint` (run from `frontend/`) → both pass with no new errors or warnings.

## Stop conditions

- Stop if a third filter control is added to this page before this plan is executed — the correct column count would need to be re-derived from the new control count, invalidating this plan's `sm:grid-cols-2`.

## Design documentation

- After acceptance and validation: none — no design documentation exists in this repository.
