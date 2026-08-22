# Wave 4 domain gaps — what persistence and the UI must add

**Status:** handoff. The Core changes are done and tested; the two items below
are **not**, because `CashFlowPlanner.Storage.Json` and the Blazor app are owned
by other agents.
**Created:** 2026-08-22
**Covers:** net-worth consolidation (gap 1) and inflation (gap 2).

---

## 1. `CashFlowPlanner.Storage.Json` — two properties, no migration

Everything else round-trips already. `CashFlowPlanDocument` holds the Core types
directly (`List<TransactionDefinition>`, `List<Pillar3aContract>`), so new
properties on those types serialize with no change. Enums are already written as
strings by `CashFlowPlanJsonOptions`, so `IndexationMode` and `RealEstateType`
land readably.

Only the two **plan-level** collections are missing.

### 1.1 `CashFlowPlanDocument.cs`

```csharp
public List<RealEstateAsset> RealEstateAssets { get; init; } = [];

public InflationAssumption Inflation { get; init; } = new();
```

`RealEstateAsset` is in `CashFlowPlanner.Core.RealEstate` (already imported by
the document). `InflationAssumption` is in `CashFlowPlanner.Core.Indexation` and
needs a new `using`.

### 1.2 `CashFlowPlanDocumentMapper.cs`

In `ToPlan()`:

```csharp
RealEstateAssets = document.RealEstateAssets ?? [],
Inflation = document.Inflation ?? new(),
```

In `ToDocument()`:

```csharp
RealEstateAssets = plan.RealEstateAssets,
Inflation = plan.Inflation,
```

### 1.3 Schema version

**No bump and no migration are needed.** Both additions are additive with safe
defaults: an old file has neither key, deserializes to an empty list and a
zero-rate assumption, and a zero rate reproduces the previous behaviour exactly.
`[JsonExtensionData]` already protects the reverse direction.

The one thing to watch: **until 1.1/1.2 land, a plan that gains real-estate
assets or an inflation rate in the UI loses them on save.** They are not in the
document, so they are not in `ExtensionData` either — the round-trip drops them
silently, which is finding P2a in a new place. Land these two properties before
shipping any UI that can set them.

### 1.4 Suggested round-trip test

Extend `StorageTestPlanFactory` with one property carrying a `ValuationDate` and
`AnnualValueGrowthPercent`, an inflation assumption with a rate and base date, a
Pillar 3a contract with a non-null `AccountId`, and a transaction with
`IndexationMode.Custom`. The existing round-trip assertions then cover all of it.

---

## 2. UI — what Core now offers, and what it needs from the user

### 2.1 Net worth (new)

`SimulationResult.NetWorthPoints` is a daily series of `NetWorthPoint`. Every
component is stored separately and signed for display — assets positive,
liabilities positive-as-owed — so a stacked chart needs no arithmetic:

| Component | Source |
|---|---|
| `LiquidAssets` | `BankAccount`, `SavingsAccount`, `Cash` balances |
| `InvestmentAssets` | `Investment` balances |
| `Pillar3aAssets` | `Pillar3a` account balances |
| `RealEstateValue` | `plan.RealEstateAssets`, compounded from `ValuationDate` |
| `MortgagePrincipal` | `MortgagePrincipalPoints` (as owed) |
| `OtherLiabilities` | `Account.IsLiability` balances, negated |

`TotalAssets`, `TotalLiabilities` and `NetWorth` are derived and sum exactly.
`SimulationResult.TryGetNetWorth(date)` gives one point.

`External` accounts are excluded on purpose. Pillar 2 (BVG), AHV and **tax** are
not modelled at all — see `TAX-MODEL.md` §8 for the disclosure this obliges.

### 2.2 Real estate (new editor required)

`CashFlowPlan.RealEstateAssets` had no UI because the collection did not exist.
Needs: name, type (House/Flat), current estimated value, optional valuation date,
optional annual value growth %, Pillar 2 (BVG) amount used, multi-select of
linked mortgages, and **optional acquisition and disposal dates**.

> **Added after the handoff was written.** `AcquisitionDate` and `DisposalDate`
> did not exist in the original design, which treated every property as owned for
> the whole horizon. That turned out to be unsafe once mortgages stopped counting
> before their `InitialDate`: a house bought in July stayed on the balance sheet
> from January while the mortgage that paid for it correctly waited, overstating
> net worth by the entire property. Both default to null, which is the original
> behaviour exactly. Label them plainly — "owned since" / "sold on" — and leave
> them empty for a property the household already lives in.

Validation to surface before save, all of which `CashFlowPlan.Validate()`
enforces by throwing:

* a disposal date must be after the acquisition date;
* a linked mortgage must exist in the plan;
* one mortgage may not be linked to two properties;
* a non-zero growth rate requires a valuation date;
* value and BVG amount may not be negative.

Growth defaults to 0, which holds the property flat — the previous behaviour and
the only assumption-free one. Do not pre-fill a growth rate.

### 2.3 Pillar 3a — the account link (this is the important one)

`Pillar3aContract.AccountId` is new and **optional**, and it is what fixes
finding H8. Without it a contribution debits the payment account and credits
nothing: the money leaves the plan.

The UI must:

* offer, on the Pillar 3a contract editor, a picker of `AccountType.Pillar3a`
  accounts, and make setting it the obvious path — ideally offering to create the
  account inline, since a user with no Pillar 3a account has nothing to pick;
* surface the `PILLAR3A_CONTRACT_NOT_LINKED` warning prominently. It is emitted
  once per unlinked contract and it means "this contract's money is being
  destroyed by the simulation";
* remember that a Pillar 3a account needs **exactly one owner and a subtype**
  (`AccountValidator` rejects it otherwise), so the inline-create flow must ask
  for both.

Plan validation rejects: an unknown account, an account that is not
`AccountType.Pillar3a`, a currency mismatch, and two contracts sharing one
account.

Note the overlap with the existing Pillar 3a projection page:
`Pillar3aProjectionEngine` projects a contract's value from `OpeningValue` plus
contributions plus assumed growth, independently of the account. Once a contract
is linked, the **account balance** is what net worth uses. The two are separate
views of the same money and will disagree if the account's opening balance and
the contract's `OpeningValue` disagree — worth showing side by side rather than
picking one.

### 2.4 Pillar 3a withdrawals (now simulated)

`Pillar3aContract.Withdrawals` was already editable, validated and persisted, and
was never simulated. It is now. Consequences for the editor:

* a withdrawal with `CloseContract` **stops the contract's contributions** on
  that date — say so next to the checkbox;
* a closing withdrawal with no stated amount sweeps the balance. If the linked
  account bears interest, the sweep excludes it and
  `PILLAR3A_CLOSE_IGNORES_GROWTH` is raised; the fix the warning asks for is
  entering an explicit amount;
* a withdrawal with neither a linked contract account nor a `TargetAccountId`
  raises a **critical** `PILLAR3A_WITHDRAWAL_NOT_POSTED` and produces no event.

### 2.5 Inflation (new plan-level setting + per-transaction field)

Plan settings gain `CashFlowPlan.Inflation`:

* annual rate %, default 0 — **leave it at 0 by default**; a pre-filled rate
  would change every existing plan's numbers the moment the user opens it;
* base date, **required as soon as the rate is non-zero** (validation throws
  otherwise). Label it as the date the plan's amounts are stated in the money of
  — "today's francs" for most users.

Transaction editor gains three fields, sensibly behind a disclosure:

* `IndexationMode`: *Follow plan* (default) / *Not indexed* / *Own rate*;
* `AnnualIndexationRatePercent`, shown and required only for *Own rate*;
* `IndexationBaseDate`, optional override of the plan's base date.

Guidance worth putting in the UI: rent, groceries and insurance follow the plan;
a fixed-rate mortgage instalment does not; a salary uses its own rate. Salary
progression needs no separate feature — an income with its own rate *is* salary
progression.

### 2.6 Real vs nominal — a toggle, not a setting

Every amount the engine produces is **nominal**: francs of the day the money
moves. Turning inflation on does not redefine any existing number.

`SimulationResult` offers the second reading:

* `GetNetWorthPoints(AmountBasis.Real)` — the whole balance sheet deflated to the
  plan's base date, components and all;
* `ToBasis(amount, date, basis)` — for any single figure;
* `CashFlowEvent.IndexationFactor` — what an amount already carries, so a row can
  show "CHF 1'000 in 2026 money → CHF 1'486 when paid" without re-deriving
  anything.

With no inflation assumption the two bases are identical, so the toggle can be
shown unconditionally and simply does nothing until a rate is set. **Label the
axis.** A chart that switches basis without saying so is worse than one that
never offers the choice.

### 2.7 Not indexed, on purpose

Mortgage and Pillar 3a events are generated from their contracts and carry no
indexation. That is correct for a fixed-rate mortgage. For Pillar 3a it means the
contribution amount stays as entered even though the statutory maximum drifts —
that maximum is a legislative figure, not CPI, and the app does not invent one.
Say so where Pillar 3a contributions are shown.
