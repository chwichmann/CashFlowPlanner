# CashFlow Planner

A private cashflow planner for Swiss household finances. It projects your bank
balances forward day by day — salary, recurring expenses, mortgage interest and
amortisation, credit-card billing cycles, Pillar 3a contributions — so you can
see what your liquidity actually looks like in six months or six years.

**It runs entirely in your browser. Your financial data never leaves your
machine.** There is no backend, no account, no telemetry, and no network call of
any kind. Your plan lives in a JSON file that you own and control.

**Live app:** https://chwichmann.github.io/CashFlowPlanner/

---

## Why this exists

Generic budgeting apps answer "where did my money go?". This one answers
"where is my money going to be?" — and it does so with the specifics Swiss
household planning actually needs:

- **SARON mortgages** with manually entered rate curves, interpolated between
  points, with the flexible component floored at 0%
- **Quarterly bank billing periods** with business-day adjustment
- **Direct and indirect amortisation**, the latter modelled as a transfer to a
  Pillar 3a account rather than as money leaving the plan
- **Pillar 3a** contribution limits, tax-year simulation and projections
- **House-purchase affordability** against the Swiss rules: 66% first-mortgage
  threshold, 15-year amortisation to 66%, 5% imputed interest, 33% affordability
  limit, and purpose-bound Pillar 2 capital

## Features

| Area | What it does |
|---|---|
| **Accounts** | Bank, savings, credit-card and Pillar 3a accounts with opening balances, IBANs and tiered interest contracts |
| **Transactions** | One-off and recurring movements with flexible schedules (daily → yearly), business-day adjustment and categories |
| **Mortgages** | Contracts with SARON rate curves, quarterly billing, direct/indirect amortisation, internally tracked principal |
| **Credit cards** | Billing-cycle modelling with closing and payment days |
| **Pillar 3a** | Contracts, contribution schedules, limit checking, tax-year and growth projections |
| **Bank import** | MT940 statement import with deduplication, account matching and reconciliation |
| **House-buy simulator** | Swiss affordability and financing rule checks against multiple scenarios |
| **Dashboard & simulation** | Day-by-day balance projection, liquid-balance and net-worth charts, account statements |
| **Languages** | English and German, with Swiss/German/US number and date formatting |

## How your data is stored

The design is deliberately file-first:

- **A JSON plan file is the source of truth.** You export it and keep it wherever
  you like — a local folder, OneDrive, a USB stick, version control.
- **Browser storage is only a working copy.** It exists so a reload doesn't lose
  your session. It is not a substitute for exporting.
- **Nothing is uploaded, ever.** The app has no server component. Hosting it on
  GitHub Pages is just static file delivery.

> **Export your plan regularly.** The current build does not yet warn you about
> unexported changes — see *Known issues* below.

## Getting started

### Use it

Open the live app, then use the folder icon in the top-right of the navigation to
open a plan file. `samples/private-cashflow.sample.json` is a small synthetic plan
you can load to look around.

### Run it locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.400 or
later).

```bash
git clone https://github.com/chwichmann/CashFlowPlanner.git
cd CashFlowPlanner
dotnet run --project src/CashFlowPlanner.BlazorWasm
```

Then open the URL printed in the console.

### Run the tests

```bash
dotnet test CashFlowPlanner.slnx
```

## Project layout

```
src/
  CashFlowPlanner.Core          Domain model and simulation engine.
                                Accounts, Banking, Mortgages, CreditCards,
                                Pillar3a, RealEstate, Analysis, Validation.
                                No UI or storage dependencies.
  CashFlowPlanner.Storage.Json  Plan document, JSON serialization, DTO mapping.
  CashFlowPlanner.BlazorWasm    Blazor WebAssembly UI, application state,
                                localisation resources.
tests/
  CashFlowPlanner.Core.Tests
  CashFlowPlanner.Storage.Json.Tests
docs/
  STABILIZATION-PLAN.md         Current architecture assessment and roadmap.
```

`Core` has no dependency on `Storage.Json` or the UI, so the domain logic is
testable headlessly and could be reused by a different front end.

## Technology

- .NET 10 / C#, Blazor WebAssembly (client-side, trimmed)
- Bootstrap 5.3.3, vendored locally — no CDN, no external fonts
- `System.Text.Json` for persistence
- xUnit for tests
- GitHub Actions → GitHub Pages on every push to `master`

## Known issues

This project is under active stabilization. Several defects are known and being
worked through — see [`docs/STABILIZATION-PLAN.md`](docs/STABILIZATION-PLAN.md)
for the full assessment, severity ledger and roadmap.

The ones worth knowing before you rely on the numbers:

- **Simulation figures are being corrected.** Account interest is currently
  posted twice, interest ignores account opening dates, and credit-card payments
  can be dated one billing cycle early. Fixes are in progress.
- **No unsaved-changes warning.** Closing the tab can discard work that was never
  exported.
- **Long horizons are slow.** A 10-year plan with interest contracts takes many
  seconds to simulate and will block the browser tab.
- **Not yet installable or offline-capable.** Despite earlier documentation, the
  app has no web manifest or service worker. Both are planned.

## Roadmap

| Wave | Focus |
|---|---|
| 1 | Correct simulation results, eliminate silent data loss, gate deploys on tests, usable first run |
| 2 | Encrypted plan files (`age` format) and auto-save straight to a folder on disk |
| 3 | Design-token layer and shared UI components; PWA install and offline support |
| 4 | CAMT.053 bank-statement import; net worth, inflation and tax modelling |

Bank *API* access is deliberately out of scope: Switzerland has no PSD2
equivalent, SIX bLink admission requires a registered legal entity, and a
browser-only app cannot hold banking credentials. File-based import (CAMT.053 and
CSV) is the supported path instead.

## Contributing

This is a personal project, but issues and pull requests are welcome. Please run
`dotnet test` before submitting.

## License

[MIT](LICENSE) © 2026 Christian Wichmann
