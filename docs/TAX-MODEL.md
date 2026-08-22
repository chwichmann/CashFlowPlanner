# Swiss household taxes — design, and why nothing is implemented yet

**Status:** design only. No tax code exists in `CashFlowPlanner.Core` and none is
being added by this document.
**Created:** 2026-08-22
**Context:** wave 4 domain gaps, after net-worth consolidation and inflation.

---

## 1. The decision, and the reason for it

Swiss taxes are the largest single expense in most households the app is aimed
at — comfortably larger than the mortgage interest the app already models to the
day — and they are the biggest remaining hole in the domain.

They are also the one thing in this codebase that **cannot be made approximately
right**. A cash-flow number that is 5% out reads as a cash-flow number that is
5% out. A tax number that is 5% out reads as *the tax number*, and a household
that plans an early retirement against it discovers the error at the worst
possible moment. The app is offline by design (criterion S7), so it cannot
quietly refresh a tariff table when the law moves; whatever ships is what the
user gets until they update the app.

So: **a documented design beats a half-right tax calculation.** An obviously
absent number provokes the user to look their real tax bill up. A plausible
wrong one does not.

What tips the balance is not difficulty but *reference data*. The engine work is
ordinary; see §6. The blocker is that a correct Swiss income-tax figure needs
three tariff layers and roughly 2,100 communal multipliers, none of which are in
this repository, all of which change annually, and none of which can be honestly
guessed. Section 5 says exactly what would have to be sourced and maintained.

**What this means in practice:** until the data pipeline in §5 exists, the
net-worth series and the cash flow are *pre-tax*. That must be stated in the UI
next to every long-horizon figure, not buried in a tooltip. See §8.

---

## 2. What a Swiss household actually owes

Four separate charges, on two different bases, assessed by three levels of
government.

### 2.1 Income tax — federal (direct federal tax, dBSt/IFD)

* Progressive tariff on taxable income, applied to the whole of Switzerland.
* Separate tariffs for single filers and for married couples / single parents.
  Married couples are **assessed jointly**: their incomes are added and taxed on
  the married tariff. There is no option to file separately.
* No federal wealth tax exists. The federation taxes income only.
* The tariff brackets are indexed for cold progression periodically, so a
  hard-coded table decays.

### 2.2 Income tax — cantonal and communal

This is where most of the money is, and where all the variation is.

* The canton publishes a **base tariff** (*einfache Staatssteuer* /
  *impôt cantonal simple*) — progressive, and structurally different from canton
  to canton, not merely differently scaled.
* Canton and commune each apply a **multiplier** (*Steuerfuss* / *coefficient*)
  to that base amount. Two communes in the same canton with the same income
  differ purely by their multiplier, and the spread within a canton is large.
* **Church tax** is a further multiplier, levied only on members of a recognised
  church, and only in cantons that levy it. It is optional per person and must be
  modelled as such — assuming it is a common and material error.
* Deductions differ between the federal and the cantonal computation. The same
  household has **two different taxable incomes** in the same year. Any model
  that computes one taxable income and applies three tariffs to it is wrong by
  construction.

### 2.3 Wealth tax — cantonal and communal only

* Levied on **net wealth** (assets minus debts) at 31 December.
* Cantonal base tariff plus the same canton/commune multipliers.
* Each canton grants a tax-free allowance, varying by marital status and number
  of children.
* Securities and bank balances at market/closing value; real estate at the
  **cantonal tax value** (*Steuerwert* / *valeur fiscale*), which is a formal
  assessed figure and is typically well below market value. The app's
  `RealEstateAsset.CurrentEstimatedValue` is a *market* estimate and must not be
  fed to a wealth-tax calculation directly — that would overstate the tax. A
  separate `TaxValue` field is required. See §4.

### 2.4 Capital-payout tax on Pillar 3a withdrawal

* A Pillar 3a payout is **not** added to ordinary income. It is taxed separately,
  once, at a reduced rate, federally and cantonally.
* The rate is progressive in the size of the payout, so **staggering withdrawals
  across several tax years reduces the total** — which is precisely the kind of
  question this app should be able to answer, and the strongest argument for
  eventually building §3.4.
* Now that `Pillar3aEventGenerator` generates withdrawals (wave 4, gap 1), the
  *event* this charge attaches to finally exists. The charge itself does not.

---

## 3. Property: Eigenmietwert and its offsets

This is the part most specific to Switzerland, and the part most likely to be
modelled wrongly.

### 3.1 The mechanism

An owner-occupier is taxed on the rent they do not pay themselves.

* **Eigenmietwert** (imputed rental value) is **added to taxable income**, as if
  the household were its own tenant.
* **Mortgage interest** is deductible from income (private debt interest is
  deductible against investment income plus a cap — the exact cap is a federal
  figure that must be sourced, not remembered).
* **Maintenance** is deductible either as actual documented costs or as a **flat
  percentage of the Eigenmietwert**; the flat rate differs by the age of the
  building, and cantons may set their own alongside the federal option. The
  taxpayer may choose the more favourable option, **and may choose differently
  each year**. A model that silently picks one is choosing for the user.
* Value-adding renovations are *not* deductible as maintenance; energy-efficiency
  work generally is, under separate rules. This distinction is a common source of
  wrong numbers and would need to be surfaced, not inferred.

The net effect is the counter-intuitive one that makes this worth modelling at
all: **paying a mortgage down raises the tax bill**, because the interest
deduction shrinks while the Eigenmietwert does not. A tool that tells a user to
amortise faster without showing that effect is giving incomplete advice. The app
already models direct vs indirect amortisation as a choice — and indirect
amortisation exists in the first place *because* of this tax asymmetry.

### 3.2 The reform risk — the strongest single reason to wait

The abolition of the Eigenmietwert for owner-occupied primary residences was
accepted in a federal popular vote in **September 2025**, as part of a package
that also restricts the deductibility of private debt interest and introduces a
federal property tax on second homes. Entry into force was **not** simultaneous
with the vote and is expected only after the cantons have adapted, plausibly
around 2028; transitional rules will matter.

**Verify the current status and the confirmed commencement date before writing
any Eigenmietwert code.** The details in this section are as understood at the
time of writing and are exactly the kind of thing that goes stale.

The consequence for sequencing is blunt: a model built today around
Eigenmietwert plus interest deduction encodes a regime with a known expiry date,
for a tool whose entire purpose is a 20-to-30 year horizon. Any implementation
must therefore be **regime-dated from the first line** — the rules in force are a
function of the tax year, not a constant — which is more design work than the
present-day calculation itself.

### 3.3 The Pillar 3a deduction the app computes and discards

`Pillar3aTaxYearSimulator` already computes, per person per tax year, the
scheduled contributions against the annual limit, and reports `Remaining`,
`Excess` and `IsLimitReached`. That is the *input* to the deduction and it is
thrown away: nothing subtracts it from a taxable income, because there is no
taxable income to subtract it from.

Two further rules that a naive implementation gets wrong:

* The limit depends on whether the person is affiliated to a pension fund
  (2nd pillar). `Person.Pillar3aEligibility` already carries this distinction.
  Without a pension fund the limit is a *percentage of earned income* capped at a
  much higher absolute figure, so it depends on income and cannot be a constant.
* The deduction requires **earned income in that year**. A person with no earned
  income may not contribute at all, so a schedule that keeps running past
  retirement produces a contribution that is not merely non-deductible but not
  permitted. The app does not check this today.

### 3.4 Ordering effects worth modelling later

Once a tax engine exists, these are the questions that justify it — and none of
them can be answered by a spreadsheet of averages:

* Amortise directly or indirectly through Pillar 3a?
* Stagger Pillar 3a withdrawals across tax years, and over how many?
* Buy in to the 2nd pillar in which year?

---

## 4. Data the domain does not have yet

Required on the plan, none of it currently modelled:

| Field | On | Why | Notes |
|---|---|---|---|
| Canton | plan or person | Selects the cantonal tariff and the wealth-tax allowance | 26 values |
| Commune / municipality | plan | Selects the communal multiplier | ~2,100 values |
| Marital status, and its start/end dates | person or plan | Joint assessment; different tariff and different allowance | Changes mid-horizon; a marriage or a death re-tariffs the whole household |
| Church-tax membership | person | Optional multiplier | Per person, not per household |
| Number of dependent children, by year | plan | Child deductions and allowances | Ages matter and change every year |
| Tax value (*Steuerwert*) of each property | `RealEstateAsset` | Wealth-tax base — **not** the market estimate already stored | Assessed figure, typically well below market |
| Eigenmietwert of each property | `RealEstateAsset` | Added to taxable income | Cantonally determined; not derivable from market value by a formula the app can hold |
| Building age or construction year | `RealEstateAsset` | Selects the flat maintenance deduction rate | |
| Maintenance treatment (flat vs actual) per year | plan | The taxpayer's annual choice | Must be a choice, not a default |
| Taxable-income classification per transaction | `TransactionDefinition` | Salary is taxable; a transfer between own accounts is not; a gift below the threshold is not | This is a **new axis on every transaction**, comparable in scope to the indexation axis added for inflation |
| Pension-fund (2nd pillar) capital and buy-ins | person | Wealth-tax exempt while in the fund, but drives the Pillar 3a limit and the buy-in deduction | Not modelled anywhere today |

That last row of the transaction table is the sleeper. Roughly every existing
transaction in a saved plan would need classifying, and a wrong default silently
biases the result — which is the same failure mode as H8, one layer up.

---

## 5. Where the rates come from, and how they stay current

This is the actual blocker.

**Sources.** The Federal Tax Administration (ESTV/AFC) publishes the federal
tariffs and maintains a national tax calculator; each cantonal tax administration
publishes its own base tariff and the communal multipliers for that canton. The
ESTV also publishes machine-readable tax data. All of it is public. None of it is
in this repository.

**The four hard constraints:**

1. **Volume.** 26 cantonal tariffs × 2 or more filing statuses, plus ~2,100
   communal multipliers, plus church multipliers, plus wealth-tax tariffs and
   allowances, plus federal tariffs. This is a dataset with an owner and a
   release process, not a constant in a `.cs` file.
2. **Annual churn.** Multipliers are set yearly by each commune. Tariffs are
   revised for cold progression. A table shipped in 2026 is wrong for 2027 and
   badly wrong for 2030 — while the app's horizon is 2056.
3. **Offline by design.** Criterion S7 forbids network calls, and the wave-2
   encryption work exists precisely so nothing leaves the device. The data must
   therefore ship *inside* the app and be versioned with it, or be imported by
   the user from a file. It cannot be fetched.
4. **Payload.** Wave 2 fought to reclaim ~1 MB of ICU data. A full tariff dataset
   is not free, and every user pays for the 25 cantons they do not live in.
   Whatever ships must be splittable per canton and lazily loaded.

**The consequence.** Any implementation must be built around a
`TaxRateTable(year, canton, commune)` provider whose data is:

* versioned and dated, with the source and retrieval date recorded per entry;
* **honest about absence** — the provider must be able to answer "I do not have
  2031" and the engine must then decline to produce a figure rather than
  extrapolating the last year it has;
* updatable without shipping code, so a user in an unlisted commune can supply
  their own multiplier;
* covered by tests that assert a small number of **published reference cases** —
  income, canton, commune, expected tax — taken from an official calculator.
  Without those, the implementation is unfalsifiable.

That last point is the one that decides it. There is no way to know a Swiss tax
implementation is right except by reproducing published figures, and this
worktree has neither the tariff data nor the reference cases. Writing the
arithmetic without them would produce code that compiles, passes its own tests,
and cannot be shown to be correct.

---

## 6. How it would fit the engine

The engine side is the easy part, and it should look like the existing
generators rather than like a new subsystem.

**Shape.** A `TaxEventGenerator`, alongside `MortgageEventGenerator` and
`Pillar3aEventGenerator`, returning a `TaxGenerationResult { Events, Warnings }`.
The `Warnings` half is not optional: "no tariff for 2031", "commune not in the
dataset", "Eigenmietwert regime after the reform is an assumption" are all
things the user must see, and the wave-4 Pillar 3a work established the pattern.

**Timing.** Taxes are assessed on a **calendar year** but paid on a cantonal
instalment schedule — provisional instalments during the year, a final
settlement one to two years later. Modelling only the assessment would put the
money in the wrong year for the liquidity curve, which is the app's core output.
So the generator produces two kinds of event: provisional instalments on the
canton's schedule, and a settlement true-up when the assessment lands. The lag
must be a parameter, not a constant.

**Ordering.** Tax depends on the year's income and on year-end wealth, both of
which the simulation only knows once it has run. That makes it a second-pass
generator, like `CreditCardPaymentEventGenerator` (which reads the events before
it) and `AccountInterestEventGenerator` (which runs last). Circularity is real
but bounded: tax paid this year reduces the wealth on which *next* year's wealth
tax is assessed, not this year's, so a single forward pass per tax year
terminates. Do not iterate to a fixed point.

**Net worth.** Once a settlement is only assessed and not yet paid, it is a
liability. `NetWorthPoint` would gain a `TaxLiability` component. It is
deliberately absent today, and `NetWorthCalculator` says so in its own
documentation so nobody has to guess why the number is missing.

**Inflation.** Tariff brackets index for cold progression on their own schedule,
which is *not* the plan's inflation rate. Reusing `plan.Inflation` to drift the
brackets would be an invented number. Brackets come from the dataset, per year,
or the year is declined.

---

## 7. If this is built, build it in this order

Each step is independently useful and independently checkable. Nothing later
than step 2 should start before the dataset in §5 has an owner.

1. **Taxable-base reporting, no rates at all.** Classify transactions, add the
   missing property fields, and report *taxable income* and *taxable wealth* per
   person per year — with the Pillar 3a deduction from §3.3 finally applied, and
   the Eigenmietwert / interest / maintenance offsets shown as line items.
   Applies no tariff and claims no tax figure. This is the largest share of the
   domain work, it is verifiable against the user's own tax return line by line,
   and it is **wrong in no way at all if the tariff data never arrives**. If any
   tax work is done, it should be this.
2. **The rate-table provider**, with one canton populated and published
   reference cases as tests. Proves the shape before the data volume lands.
3. **Federal income tax**, the only layer with a single nationwide tariff.
4. **Cantonal + communal income tax**, multipliers, church tax.
5. **Wealth tax**, needing the property tax values from §2.3.
6. **Capital-payout tax** on Pillar 3a withdrawals — the step that pays for
   itself, since staggering withdrawals is a real, answerable optimisation.
7. **Instalment and settlement timing**, so the liquidity curve is right.

Step 1 alone would remove the single largest correctness caveat from the
long-horizon projection without asserting a single franc of tax.

---

## 8. Until then

State it plainly, wherever a long-horizon figure appears:

> Taxes are not modelled. Cash-flow and net-worth figures are before tax.

Not a footnote. A household reading a 2056 net-worth figure that silently omits
thirty years of income tax, wealth tax and Eigenmietwert is being misled by the
omission, and the omission is invisible unless the app says so.
