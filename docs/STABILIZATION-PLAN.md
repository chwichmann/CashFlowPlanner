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

Ordered so the PWA layer lands first (it is a prerequisite, not a nice-to-have),
then encryption, then disk writes — so the first byte ever written to the user's
disk is already ciphertext.

**2.0 — PWA layer (~1 day).** `manifest.webmanifest`, service worker, 192/512/
maskable icons, real `<title>`, `theme-color`, `<html lang>` bound to culture.
Unlocks persistent file permissions, storage persistence and file handling.

**2.1 — Encryption (4–7 days).**

Hard constraint, verified against the .NET 10.0.11 reference assemblies:
**WASM has no symmetric cipher.** `AesGcm`, `AesCcm`, `ChaCha20Poly1305`,
`Aes.Create`, `RSA.Create`, `ECDsa.Create` all carry
`[UnsupportedOSPlatform("browser")]`. `Rfc2898DeriveBytes`/`Pbkdf2`, `HKDF`,
SHA-2, HMAC and `RandomNumberGenerator` DO work. Net effect: a key can be derived
in C# but nothing can be encrypted with it. Managed crypto is also 20–40× slower
in WASM (OWASP-grade PBKDF2 ≈ 2.5–5 s). **All crypto goes through Web Crypto via
JS interop.**

Format: **adopt `age` v1** (`age-encryption.org/v1`), not a bespoke envelope.
Self-describing, versioned, header authenticated by HMAC, payload under
ChaCha20-Poly1305 STREAM with a spec-mandated fresh file key and nonce per
encryption. Browser implementation: `typage` (~92 KB bundled). Files decrypt with
the standard `age` CLI — the independent-tool property.

Key design (this is what makes autosave viable at all):

| When | Operation | Cost |
|---|---|---|
| Setup, once | generate X25519 identity; wrap it with the passphrase via age-scrypt; store wrapped blob; offer `identity.txt` download | one-time |
| Unlock, per session | scrypt once to unwrap the identity; hold in memory only | ~0.5–2 s |
| **Every autosave** | encrypt to the public recipient | **milliseconds, no KDF** |

Do NOT use age's passphrase recipient for the plan file — it re-runs scrypt at
2^18 (~256 MiB) per encryption with a fresh salt, so it cannot be cached.

UX requirements: setup blocks on passphrase-twice + `identity.txt` download +
explicit "no reset" acknowledgement; wrong passphrase fails fast via the header
MAC; idle-lock timer drops the key. Optional later: WebAuthn PRF (passkey unlock).

**Also in scope:** the localStorage working copy is plaintext financial data today
(`cashflowplanner.currentPlanJson`) — encrypt it with the same recipient and move
it to IndexedDB (localStorage is ~5 MB, synchronous, string-only). Call
`navigator.storage.persist()`; without it Safari's ITP evicts after 7 days of no
interaction.

Includes `[JsonExtensionData]` and the migration chain (P2a/P2b/P2c) if wave 1 has
not already landed them — the envelope change is a schema change.

**2.2 — Auto-save to a user folder (4–6 days). CONFIRMED POSSIBLE.**

File System Access API. Handle from `showSaveFilePicker`, persisted in IndexedDB
(in JS — a handle can never leave the JS heap as anything but an opaque
reference). Only the picker and `requestPermission()` need a user gesture;
**writing to an already-permitted handle does not**, so background autosave is
genuinely unattended. Chrome 122+ persistent permissions add "Allow on every
visit", and an **installed PWA persists permissions automatically with no prompt**.

`createWritable()` writes to a swap file and atomically replaces the original only
on `close()` — a crash mid-write leaves the original intact.

| Tier | Browsers | Experience |
|---|---|---|
| 1 | Chrome/Edge desktop, installed as PWA | pick once, silent autosave forever |
| 2 | Chrome/Edge/Opera in-tab; Chrome Android 132+ | one "Reconnect" click per session (Android: no atomic writes) |
| 3 | Firefox, Safari desktop/iOS | OPFS + IndexedDB autosave, manual export, "unsaved to disk" badge |

Firefox (negative) and Safari (oppose) hold **formal standards positions**. Do not
plan for them to change. OPFS is complementary, not a substitute — it is not
user-visible and is deleted when site data is cleared; use it as a crash journal
and a rolling backup ring.

Consequence worth stating to the user: because the file is ciphertext, the target
folder can be **OneDrive/iCloud/Dropbox** — sync and off-site backup for free,
without the provider ever seeing the finances.

Optionally add the File Handling API (`file_handlers` + `launchQueue`) so
double-clicking a plan file opens the PWA with the handle already granted.

### Wave 3 — UI system

Token layer (`tokens.css` overriding Bootstrap's own `--bs-*`), the six shared
components (`PageScaffold`, `DataTable<T>`, `FormField`, `MoneyInput`, `AppModal`,
`ConfirmDialog`), an `AppFormatter` service replacing 21 duplicated helpers,
retirement of the second design system on `HouseBuySimulator` (882 lines, 34
colors), payload reduction (B6).

Migration order matters: the quick wins (U3, U5, `::deep` for U1) land first,
then tokens, then the components — after which a new page *cannot* get its header,
table alignment or field labels wrong, because it no longer writes that markup.

### Wave 4 — Bank import (CAMT.053) and domain gaps

**Bank API access is permanently out of scope** — blocked by identity, not
technology. No PSD2 equivalent; the Federal Council confirmed on 2025-12-12 that
no regulatory requirement for open interfaces is coming. SIX bLink requires a
commercial-register legal entity with audited accounts and criminal-record
extracts of management (CHF 5,000 + CHF 200/month), and mandates mutual TLS with
an **OV/EV certificate issued to organisations, not natural persons** — which a
browser cannot present to `fetch()` under any circumstances. PKCE does not help.
Any secret in a WASM app is public (assemblies ship as `.wasm`, readable in
ILSpy).

**The real path is CAMT.053** (ISO 20022 XML, `camt.053.001.08` per Swiss Payment
Standards 2026 v2.3, tolerating `.001.04` until Nov 2026). bLink's own API returns
camt.053, so file import targets the identical shape.

The existing `Banking/Import` pipeline already provides ~80% of this — dedup key
builder, merger, fingerprinting, account matching, reconciliation. The MT940 test
fixture IBAN carries clearing number `00210` (UBS Switzerland), so the path is
proven against a real Swiss retail bank. Keep the MT940 parser; build CAMT.053
next rather than investing further in a format being retired.

Take **no NuGet dependency**: no mature, Swiss-aware, trim-safe camt package
exists, and `XmlSerializer` is a WASM trap that works in `dotnet run` and fails
only after `dotnet publish`. Hand-roll ~300–500 lines with **namespace-agnostic
`LocalName` matching**, so `.04`/`.08`/future versions share one code path.

Parser rules: `Ntry` is first-class, `TxDtls` optional enrichment — never sum
across levels (internal vs external batch bookings will double-count); `Ccy` is an
attribute; sign from `CdtDbtInd`, never a negative amount; check both
`Prtry == "QRR"` and `Cd == "SCOR"` for references; assert
`CLBD − OPBD == Σ signed Ntry.Amt` on every import; dedup on
`(IBAN, Ntry/AcctSvcrRef)` with content-hash fallback.

**CSV must be a first-class path**, not an afterthought: camt.053 behaves as a
business-banking feature at most Swiss retail banks and does not exist at the
neobanks (neon, Yuh, Zak). Build declarative per-bank column-mapping profiles with
a preview/confirm screen, not one class per bank.

Then the domain gaps, ranked by value: net worth consolidation, inflation, taxes
(Eigenmietwert, wealth tax, the Pillar 3a deduction the app already computes and
discards), scenarios/what-if, salary progression, mortgage rollover.

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
