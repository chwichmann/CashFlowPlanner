# CashFlowPlanner — Stabilization Plan

**Status:** active
**Created:** 2026-08-21
**Baseline commit:** `151eee5`

This plan takes CashFlowPlanner from "deploys, but computes wrong numbers and can
silently lose your work" to a defensible stable version. It is derived from five
independent architecture reviews (domain, UI, persistence/state, build/CI,
feasibility). Every finding referenced here was reproduced by executing the code
or by direct measurement in the browser — none are inferred.

---

## 1. Definition of "stable"

Stable is a checkable state, not a feeling. The bar:

| # | Criterion | How it is verified |
|---|---|---|
| S1 | The simulation reports correct money | Regression tests for C1–C4 pass; end-to-end `Simulate()` test with an interest contract asserts exact balances |
| S2 | No silent data loss | Autosave failures surface in the UI; unsaved-changes indicator + `beforeunload`; no reachable unsavable state |
| S3 | Nothing broken can deploy | CI runs build + tests + warnings-as-errors before publish; base-href rewrite is asserted, not hoped for |
| S4 | A first-time user can start | "New plan" and "Load sample" exist; no dead-end empty session |
| S5 | The UI is internally consistent | One token layer; shared page/table/form components; no left/right column drift |
| S6 | The repo explains itself | Real README, LICENSE, accurate PROJECT_STATE |
| S7 | It stays self-contained | No network calls; verified by test, not by inspection |

Wave 1 delivers S1–S4, S6, S7. Wave 2 delivers encryption and disk auto-save.
Wave 3 delivers S5 in full plus the PWA layer.

---

## 2. Severity ledger

### Correctness — the numbers are wrong today

| ID | Defect | Measured impact |
|---|---|---|
| **C1** | Account interest generated twice (`SimulationEngine.cs:135` + `CashFlowEventGenerator.cs:69-81`) | CHF 100k @ 1% → two 1'013.89 events; balance 102'027.78 instead of 101'013.89. Every interest-bearing account ~2× overstated |
| **C2** | Interest ignores `Account.OpeningDate` (`AccountInterestEventGenerator.cs:127`) | Account opened 01.12 earns a full year, not 31 days: 1'013.89 vs 86.11. With C1, 24× |
| **C3** | Credit-card payment dated in the closing month (`CreditCardPaymentEventGenerator.cs:59-62`) | Bank debited 20 days before the statement closes. Daily liquidity curve wrong by a full billing cycle |
| **C4** | `Distinct()` discards business-day collisions (`ScheduleOccurrenceGenerator.cs:89,132,184,222`) | Daily schedule over Jan 2026: 22 occurrences instead of 31. Daily expense understated 29% |
| **H1** | `CalculationPrincipalDate` in the future ⇒ mortgage reports zero debt, skips interest | H1-2026 interest (~5'300) never charged |
| **H2** | Past `CalculationPrincipal` never rolled forward | 5 × 9'000 = 45'000 amortisation ignored; all later interest on an inflated base |
| **H6** | Interest generation is quadratic | 10-year plan: 22.6 s native; WASM is single-threaded and slower ⇒ frozen tab |
| **H7** | No currency enforcement; `Money` type unused | USD 1'000 into a CHF account adds 1'000 to CHF, no warning |

### Data loss

| ID | Defect | Consequence |
|---|---|---|
| **P1a** | `DeleteAccount` is the only delete that skips plan validation (`CashFlowAppState.cs:93-148`) | Deleting an account referenced by a Pillar 3a schedule makes the plan unsavable **and** unexportable. Session unrecoverable |
| **P1b** | Autosave is fire-and-forget with no `catch` (`PlanCacheCoordinator.cs:94-97`) | Quota/validation failures are invisible; navbar still shows "cached HH:MM:SS" while nothing is written |
| **P1c** | No dirty tracking, no `beforeunload` | Closing the tab silently discards work |
| **P2a** | No `[JsonExtensionData]` | Round-trip loses `createdAt`, `modifiedAt`, `notes` on the shipped sample today |
| **P2b** | Schema version is a hard gate with no migration | A v2 file is flatly rejected by a stale cached build |
| **P2c** | `dateMode` defaults to `RollingHorizon`, overriding stored dates | UI displays 2026-06-01→2031-12-31; engine simulates a rolling 12 months |
| **P3a** | Bank-import state lives only in localStorage, never exported | Reconciled imports destroyed by clearing site data; unbounded growth can starve the plan key |

### Delivery

| ID | Defect |
|---|---|
| **B1** | No `dotnet test` anywhere in CI — 223 tests never gate a deploy |
| **B2** | `deploy.yml:42` base-href `sed` is unasserted; a non-match deploys a blank site green |
| **B3** | No `concurrency` group; racing pushes can publish stale content |
| **B4** | `pages: write` + `id-token: write` at workflow scope, held by the job that runs `dotnet restore` |
| **B5** | Warnings not errors — a live `CS8602` in `Transactions.razor:600` |
| **B6** | ~40% of the 28 MB payload is waste (9.6 MB `.gz`/`.br` Pages never serves, ~6 MB Bootstrap source maps, 1.53 MB full ICU) |

### UI — measured in the browser

| ID | Defect | Measurement |
|---|---|---|
| **U1** | Scoped CSS never reaches `<Input*>` components | **0 of 10** currency inputs carry `b-q50tm9kis7`; 27px unstyled vs 34px styled siblings; numbers left-aligned despite `text-align:right` |
| **U2** | Navbar and content never share a left edge | **65.5px** apart at 1710px viewport |
| **U3** | Transactions totals row off by one column | `colspan="8"` where the rows above use `9` |
| **U4** | Money columns left-aligned on 2 pages, right on 7; no `tabular-nums` anywhere | — |
| **U5** | `@key` on `MainLayout` rebuilds the whole page on all 28 state mutations | Resets filters, tab, scroll, focus, in-progress edits |
| **U6** | Zero design tokens | 55 colors, **5** primary blues, 22 spacings, 16 font sizes, 8 radii, 5 non-Bootstrap breakpoints |
| **U7** | 2.6% of markup is shared components | 141 labels / 19 with `for=`; 30 tables / 2 responsive; 21 duplicated format helpers |
| **U8** | Not a PWA despite the claim | No manifest, no service worker, orphaned `icon-192.png` |

### Test integrity

- `Pillar3aProjectionEngineTests.cs` contains **zero tests** — it is a stale verbatim copy of the production class, shadowing the real type while appearing covered.
- `SimulationEngineTests` never constructs an interest contract (why C1/C2 survived).
- Every credit-card test uses closing 15 / payment 25 — the one config where C3 cannot fire.
- Zero `[Theory]`/`[InlineData]` in 223 tests.
- No test project at all for the 12'900-line Blazor app.

---

## 3. Waves

### Wave 1 — Stability (S1–S4, S6, S7)

Ships correct numbers, no silent loss, a real deploy gate, a usable first run.

1. **Correctness**: C1, C2, C3, C4 — regression test written *before* each fix.
2. **Shared posting function**: extract `Apply(accountId, event)` used by
   `SimulationEngine`, `AccountStatementBuilder`, `AccountInterestEventGenerator`
   (closes H4/H5 permanently — three re-implementations of balance arithmetic).
3. **Data loss**: P1a, P1b, P1c; confirm-on-destructive-action.
4. **CI gate**: B1, B2, B3, B4, B5.
5. **Empty session**: wire up `CashFlowPlanFactory.CreateEmpty()`, ship a starter
   plan, shared empty-state with New / Open / Sample.
6. **Docs**: real README, LICENSE, corrected PROJECT_STATE (drop the PWA claim
   until it is true).
7. **Tests**: delete the shadow test file; add the bUnit project; add a
   self-containment test asserting no outbound calls.

### Wave 2 — Encryption and disk auto-save

Scope fixed by the feasibility study. Format change lands on a stable base, never
before it. Includes `[JsonExtensionData]` + a real migration chain (P2a/P2b/P2c)
because the envelope change is a schema change.

### Wave 3 — UI system and PWA

Token layer, the six shared components, retirement of the second design system on
`HouseBuySimulator`, manifest + service worker, payload reduction (B6).

### Wave 4 — Domain gaps

Net worth consolidation, inflation, taxes, scenarios — ranked by value in the
domain review. Not stability work; sequenced after.

---

## 4. Working agreements

- **Test before fix** for every correctness defect. The test must fail first.
- **No push without green** `dotnet test` locally.
- **Commit per finding**, message references the finding ID.
- **`master` deploys on push** — treat every push as a release.
- Verification of UI work is by **browser measurement**, not by build success.
