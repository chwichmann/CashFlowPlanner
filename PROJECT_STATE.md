# CashFlowPlanner Project State

Working notes on what exists and what is decided. For the full architecture
assessment and roadmap see [`docs/STABILIZATION-PLAN.md`](docs/STABILIZATION-PLAN.md).
For user-facing documentation see [`README.md`](README.md).

**Last verified:** 2026-08-21 against commit `9f6b3eb`.

## Goal

Private Blazor WebAssembly cashflow simulator for Swiss household finances,
using a JSON file as the source of truth. Fully client-side: no backend, no
network calls, no telemetry.

Installable and offline-capable as of 2026-08-22: `manifest.webmanifest` plus a
content-hash-versioned service worker. Verified by publishing Release, loading
it, stopping the server, and reloading a deep link. The icons are the app's own
mark; the previous ones were the stock Blazor logo.

The worker deliberately does **not** call `skipWaiting()`. A new deploy installs
in the background and takes over on the next load, because swapping framework
files underneath a running WebAssembly app means mismatched `.wasm` and a hard
failure mid-session.

## Projects

- `CashFlowPlanner.Core` — domain model and simulation engine
- `CashFlowPlanner.Storage.Json` — plan document, serialization, DTO mapping
- `CashFlowPlanner.BlazorWasm` — UI, application state, localisation
- `CashFlowPlanner.Core.Tests`
- `CashFlowPlanner.Storage.Json.Tests`

`Core` depends on neither storage nor UI.

## Source of truth

- JSON plan file, exported and managed by the user
- Browser localStorage holds only a temporary working copy
- OneDrive / manual export workflow

## Implemented — Core

- Accounts, with tiered interest contracts and bank identifiers
- Transactions and schedules (Once → Yearly, business-day adjustment)
- SimulationEngine, day-by-day balance projection
- MortgageContract with internally tracked principal, MortgageEventGenerator
- CreditCardContract and CreditCardPaymentEventGenerator
- Pillar 3a: contracts, contribution limits, tax-year simulation, projections
- RealEstate and Swiss house-buy affordability rules
- Banking: MT940 parser plus an import pipeline (dedup keys, merging,
  fingerprinting, account matching, reconciliation)
- `AccountPosting` — the single shared implementation of event-to-balance
  arithmetic, used by the engine, the statement builder and the interest and
  credit-card generators

## Implemented — UI

Home (import/export), Dashboard, Simulation, Accounts, Account statement,
Transactions, Mortgages, Credit cards, Pillar 3a, Persons, Bank import,
House-buy simulator, Settings. Horizontal top navigation, browser cache
restore/autosave, English and German (711 resx keys, in sync).

## Decisions

- A mortgage is not an Account; principal is tracked via `MortgagePrincipalPoints`
- A credit card remains an `AccountType`; `CreditCardContract` defines only the
  billing/payment rule
- SARON rates are entered manually and interpolated; the flexible component is
  floored at 0%
- Interest is generated exactly once, last, after credit-card payments are known
  — it depends on the running balance. `CashFlowEventGenerator` deliberately
  knows nothing about interest
- A credit-card payment day *earlier than* the closing day rolls to the following
  month. A payment day *equal to* the closing day is rejected, because the
  settled amount would depend on intra-day event ordering
- Business-day adjustment moves a payment; it never cancels one. Several nominal
  dates collapsing onto one business day are collisions, not duplicates

## Tests

292 passing (273 Core, 19 Storage.Json). No UI test project yet; bUnit is
planned for the state and cache layer, Playwright deferred to wave 3.

## Open tasks

Tracked in detail in `docs/STABILIZATION-PLAN.md`. Current headlines:

- **Wave 1 (in progress):** silent data loss (no dirty tracking, swallowed
  autosave failures, the `DeleteAccount` unsavable-state trap), CI test gate,
  empty-session entry point
- **Known correctness work not yet started:** mortgage `CalculationPrincipalDate`
  semantics (H1/H2), quadratic interest generation (H6 — a 10-year plan takes
  ~22 s), currency enforcement (H7 — the `Money` type exists and is unused)
- **Wave 2:** encrypted plan files (`age`), auto-save to a user folder, PWA layer
- **Wave 3:** design tokens and shared UI components
- **Wave 4:** CAMT.053 import; net worth, inflation and tax modelling
