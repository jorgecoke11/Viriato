# Caso detail page heading matches sibling page-heading size

Written against: ba2cac26f146559bbbfa446162655567f6fbb62b

## Evidence chain

- Surface: `/casos/:id` route → `CasoDetailPage` → renders `CasoDetailContent`
- Problem: The case detail page's only top-level heading (`{caso.titulo}`) renders at `text-xl`, one step smaller than the `text-2xl` page heading used by every sibling route in the same Casos surface.
- Design evidence: sibling page headings all use `text-2xl font-semibold text-gray-900` — `frontend/src/features/casos/pages/DashboardPage.tsx:45` ("Panel de casos"), `frontend/src/features/casos/pages/CasosListPage.tsx:41` ("Casos"), `frontend/src/features/casos/pages/NuevoCasoPage.tsx:75` ("Nuevo caso"). Confirmed rendered: "Expediente 001" on `/casos/:id` visibly renders smaller than "Casos" on `/casos/lista`.
- Owner: `frontend/src/features/casos/CasoDetailContent.tsx:111` (no shared page-heading component exists; each page sets its own `<h1>` classes directly)
- Scope and affected surfaces: `/casos/:id` only — `CasoDetailContent` is rendered only from `CasoDetailPage`
- Uncertainty: none

## Design decision

Change the `<h1>` on the case-detail screen from `text-xl` to `text-2xl` so it matches the page-heading size already established by every other page in the Casos surface. One-property correction; no other classes on that element are affected.

## Reuse

- `text-2xl font-semibold text-gray-900` (existing Tailwind utility combination, already used identically by three sibling pages)
- Exemplar: `frontend/src/features/casos/pages/CasosListPage.tsx:41`

No new primitive is required.

## Changes

1. `frontend/src/features/casos/CasoDetailContent.tsx:111`
   - Change: replace `className="text-xl font-semibold text-gray-900"` with `className="text-2xl font-semibold text-gray-900"` on `<h1>{caso.titulo}</h1>`.
   - Preserve: `font-semibold text-gray-900`, the surrounding flex layout, and the status badge / "Datos de negocio" button placement.
   - Verify: rendered at `/casos/:id`, the case title visually matches the heading size on `/casos`, `/casos/lista`, and `/casos/nuevo`.

## Scope

- Inherit: only the case-detail heading in `CasoDetailContent.tsx`.
- Verify: the header `Card` on `/casos/:id` still fits the title, badge, and "Datos de negocio" button on one row at the `sm:grid-cols-4` breakpoint used just below it; check a long `caso.titulo` value doesn't wrap awkwardly against the badge at narrow widths.
- Exclude: headings on other surfaces (`TrabajoDetailPage`, `CompanyDetailPage`, etc.) — not audited here.

## Validation

- Product: open any caso from `/casos/lista`; the detail page's title should read at the same size as the "Casos" list heading.
- Interface: check `/casos/:id` for a caso with a short title and one with a long title, at mobile width (375px) and desktop width, to confirm no wrapping regression against the status badge.
- System: confirm no other place depends on this heading being `text-xl` — `CasoDetailContent` is only rendered from `CasoDetailPage`.
- Repository: `npx tsc -b --noEmit && npm run lint` (run from `frontend/`) → both pass with no new errors or warnings.

## Stop conditions

- Stop if the title and status badge no longer fit on one line at the `sm` breakpoint after the size increase — that would require a layout change beyond this one-class correction, which is out of scope for this plan.

## Design documentation

- After acceptance and validation: none — no design documentation exists in this repository; this plan brings the surface into conformance with an existing de-facto pattern rather than establishing a new documented rule.
