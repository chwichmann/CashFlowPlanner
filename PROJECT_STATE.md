# CashFlowPlanner Project State

## Goal
Private Blazor WebAssembly PWA cashflow simulator using JSON file as source of truth.

## Architecture
- CashFlowPlanner.Core
- CashFlowPlanner.Storage.Json
- CashFlowPlanner.BlazorWasm
- CashFlowPlanner.Core.Tests
- CashFlowPlanner.Storage.Json.Tests

## Source of Truth
- JSON plan file
- Browser localStorage only as temporary working copy
- OneDrive/manual export workflow

## Implemented Core
- Accounts
- Transactions
- Schedules
- SimulationEngine
- MortgageContract with internal principal
- MortgageEventGenerator
- CreditCardContract
- CreditCardPaymentEventGenerator

## Important Decisions
- Mortgage is not an Account.
- Mortgage principal is tracked internally via MortgagePrincipalPoints.
- CreditCard remains an AccountType.
- CreditCardContract only defines billing/payment rule.
- SARON rates are manually entered and interpolated.
- SARON flexible component is floored at 0%.

## UI Implemented
- Home import/export
- Dashboard
- Simulation
- Accounts CRUD
- Transactions CRUD
- Mortgages CRUD
- Credit Cards CRUD
- Settings
- Browser cache restore/autosave
- Horizontal top navigation

## Tests
- 48 tests green as of last checkpoint.

## Open Tasks
- CreditCard payment date if payment day <= closing day should use following month.
- Plan warnings for duplicate manual credit-card payments.
- Unsaved/export-needed status.
- Net worth chart with mortgage principal.
- Mortgage principal chart.
- Sample JSON cleanup.