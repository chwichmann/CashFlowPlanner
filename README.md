# CashFlow Planner

A private cashflow planner for Swiss household finances. It projects your bank
balances forward day by day — salary, recurring expenses, mortgage interest and
amortisation, credit-card billing cycles, Pillar 3a contributions — so you can
see what your liquidity actually looks like in six months or six years.

**It runs entirely in your browser. Your financial data never leaves your
machine.** There is no backend, no account, no telemetry, and no network call of
any kind. Your plan lives in a JSON file that you own and control.

**Live app:** https://chwichmann.github.io/CashFlowPlanner/

It installs as a desktop or mobile app and works fully offline — once loaded,
you can plan on a plane with no connection at all.

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
| **Mortgages** | Contracts with SARON rate curves, monthly to yearly billing, a configurable day-count convention, direct/indirect amortisation, internally tracked principal |
| **Credit cards** | Billing-cycle modelling with closing and payment days |
| **Pillar 3a** | Contracts linked to the account they feed, contribution schedules, withdrawals, limit checking, tax-year and growth projections |
| **Real estate** | Properties with an optional valuation date, assumed growth and ownership dates, linked to the mortgages that finance them |
| **Bank import** | CAMT.053, MT940 and CSV statement import with deduplication, account matching and reconciliation |
| **Inflation** | A plan-level rate with a stated base date, per-transaction opt-out or override, and a real / nominal toggle |
| **House-buy simulator** | Swiss affordability and financing rule checks against multiple scenarios |
| **Dashboard & simulation** | Day-by-day balance projection, a net-worth balance sheet broken into its components, liquid-balance and net-worth charts, account statements |
| **Languages** | English and German, with Swiss/German/US number and date formatting |

## How your data is stored

The design is deliberately file-first:

- **A JSON plan file is the source of truth.** You export it and keep it wherever
  you like — a local folder, OneDrive, a USB stick, version control.
- **Browser storage is only a working copy.** It exists so a reload doesn't lose
  your session. It is not a substitute for exporting.
- **Nothing is uploaded, ever.** The app has no server component. Hosting it on
  GitHub Pages is just static file delivery.
- **The plan file can be encrypted** with a passphrase, and the browser working
  copy always is — with a device key that never leaves the browser's key store.
  The formats are specified in
  [`docs/ENCRYPTED-FILE-FORMAT.md`](docs/ENCRYPTED-FILE-FORMAT.md) and
  [`docs/WORKING-COPY-ENCRYPTION.md`](docs/WORKING-COPY-ENCRYPTION.md), and
  `tools/decrypt-plan.html` opens an encrypted file offline with no dependency on
  this program still existing.
- **On Chrome and Edge the app can auto-save straight to a folder** you pick, so
  the file on disk stays current without an export step. Other browsers keep the
  manual export.

> **Export your plan regularly.** The app shows an "export needed" badge while
> you have changes that exist only in this browser, and warns before you close
> the tab — but clearing site data still discards them. The file is the backup.

## Getting started

### Use it

Open the live app. With no plan loaded you are offered three ways to start:
create an empty plan, open a plan file you already have, or load a small
made-up example household to look around.

To install it as an app, use your browser's install action (Chrome and Edge show
one in the address bar). Installed, it works offline and remembers the folder you
save to.

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

The browser smoke tests are the exception: they drive a real Chromium against the
*published* output, so they need something published to point at, and they skip
with a printed reason if you do not.

```bash
dotnet publish src/CashFlowPlanner.BlazorWasm -c Release -o /tmp/pub
CFP_PUBLISH_WWWROOT=/tmp/pub/wwwroot dotnet test tests/CashFlowPlanner.SmokeTests
```

They exist because twice a green build and a full unit suite both missed a change
that left the deployed site blank. CI sets `CFP_SMOKE_REQUIRED=1`, where a skip is
a failure.

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
  CashFlowPlanner.BlazorWasm.Tests   Component tests (bUnit).
  CashFlowPlanner.SmokeTests         Playwright, against published output.
docs/
  STABILIZATION-PLAN.md         Architecture assessment, severity ledger,
                                and what each finding resolved to.
  ENCRYPTED-FILE-FORMAT.md      The plan file format, specified well enough
                                to write an independent decryptor.
  WORKING-COPY-ENCRYPTION.md    The browser working copy, which is a
                                different problem with a different answer.
  TAX-MODEL.md                  Swiss tax: designed, deliberately not built.
tools/
  decrypt-plan.html             Offline recovery. No network, no dependencies.
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

- **Cross-currency plans are only partly modelled.** Mismatched currencies are
  now rejected or flagged rather than silently added together, but there is no
  FX conversion: keep one currency per plan.
- **No tax modelling.** This is deliberate rather than pending: a correct Swiss
  figure needs 26 cantonal tariffs and roughly 2,100 communal multipliers that
  change annually and cannot be fetched by an app that makes no network requests,
  and there are no published reference cases here to check an implementation
  against. The reasoning and a build order are in
  [`docs/TAX-MODEL.md`](docs/TAX-MODEL.md). Net worth excludes tax owed, and says
  so on screen.
- **Pillar 2 (BVG) and AHV are not modelled** and are excluded from net worth. A
  guessed pension capital is worse than an absent one.
- **The simulation granularity setting does nothing.** It is editable and unread;
  see the stabilization plan.

## Roadmap

| Wave | Focus |
|---|---|
| 1 | Correct simulation results, eliminate silent data loss, gate deploys on tests, usable first run |
| 2 | Encrypted plan files (`age` format) and auto-save straight to a folder on disk |
| 3 | Design-token layer and shared UI components |
| 4 | CAMT.053 and CSV bank-statement import; net worth, inflation and real estate |
| 5 | Scenarios and what-if comparison; taxable-base reporting (see `docs/TAX-MODEL.md`) |

No CSV profile is named after a bank. None has been checked against a real export
from one, and a profile carrying a bank's name would be trusted because of the
name — so the built-in profiles are named after the shape of the file instead, and
auto-detection, which tests every guess against every row, is the default.

Bank *API* access is deliberately out of scope: Switzerland has no PSD2
equivalent, SIX bLink admission requires a registered legal entity, and a
browser-only app cannot hold banking credentials. File-based import (CAMT.053 and
CSV) is the supported path instead.

## Contributing

This is a personal project, but issues and pull requests are welcome. Please run
`dotnet test` before submitting.

## License

[MIT](LICENSE) © 2026 Christian Wichmann
