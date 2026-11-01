# Quickstart: Approval Limits by Role

## Prerequisites

- .NET 8 SDK
- Node.js and npm
- Playwright Chromium installed for the E2E project
- Stryker.NET 4.13.0 available as a .NET tool for mutation testing

## Run unit tests

From the repository root:

```powershell
dotnet test backend/InvoiceApp.sln
```

Expected result: all xUnit tests pass, including the inclusive limits, role checks,
high-value second approval, case-insensitive/trimmed user identity, and unchanged-state
assertions for rejection.

## Run mutation testing

From `backend/tests/InvoiceApp.Tests`:

```powershell
dotnet stryker
```

Expected result: approval-rule mutants are killed or individually explained in the
mutation report.

## Run the rejected over-limit browser test

Start the app from the repository root:

```powershell
dotnet run --project backend/src/InvoiceApp.Api
```

In another terminal:

```powershell
Set-Location e2e
npm test
```

The Playwright test signs in using the local demo Manager account, attempts to approve
the pending 50,001 invoice, and verifies that the limit reason is visible and the
invoice remains pending.

## Run the approval flow manually

1. Sign in with `demo.manager` or `demo.director` using the local demo password.
2. Submit a draft invoice for approval.
3. Verify that a Manager can approve 50,000 but receives a clear rejection for 50,001.
4. Verify that a Director can approve 500,000 alone.
5. For an invoice above 500,000, record the first approval and confirm it remains
   pending; sign in as a different Director and complete the second approval.
6. Attempt a duplicate user ID with different casing or whitespace and confirm that it
   is rejected without changing the invoice.
