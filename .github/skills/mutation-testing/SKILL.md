---
name: mutation-testing
description: How to run Stryker mutation testing on the .NET 8 test project, record before/after scores, and kill surviving mutants. Use when measuring or improving the quality of the approval tests.
---

# Mutation testing skill

## Install (exact version required on .NET 8)

```powershell
dotnet tool install -g dotnet-stryker --version 4.13.0
```

The newest release targets a later runtime and fails to install, so do not omit the version.

## Run

```powershell
cd backend\tests\InvoiceApp.Tests
dotnet stryker 2>&1 | Tee-Object -FilePath ..\..\..\mutation-after.txt
```

Reports are written to `StrykerOutput\<timestamp>\reports\` (HTML and JSON). `StrykerOutput/` is git-ignored.

## Record scores

- Before the feature: save the output as `mutation-before.txt` and note the line `The final mutation score is NN.NN %`.
- After the feature: save as `mutation-after.txt`.
- Paste both scores as plain text in `ASSIGNMENT.md`.

## Killing survivors

Look at surviving mutants in the report, focusing on `ApprovalService`:

| Surviving mutant | Likely missing test |
| --- | --- |
| `<=` changed to `<` on a limit | Test at exactly 50,000 or 500,000 |
| `<` changed to `<=` on a limit | Test at 50,001 or 500,001 |
| Condition negated or removed in the same-user check | Same-user-twice test with an unchanged-state assertion |
| Role check removed | Test that Manager as second approver is rejected |
| Statement removed (state change) | Test that asserts the approved state really was saved |

Add a test for each survivor and re-run. If a survivor is genuinely equivalent (changes nothing observable), explain it in `ASSIGNMENT.md` instead of adding a pointless test.
