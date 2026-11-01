## Summary

Adds approval limits by role to the invoice approval service.

- A Manager may approve invoices up to 50,000 (inclusive).
- A Director may approve invoices up to 500,000 (inclusive).
- Above 500,000 an invoice needs two distinct approvers, and the second must be a Director.
- The same user (compared by trimmed, case-insensitive user ID) never counts as both approvers.
- A rejected attempt returns a specific reason and leaves the invoice unchanged. All rules run before any state change.

The `Approver` role is replaced by `Manager` and `Director`. Admin cannot approve. Demo accounts are `demo.manager`, `demo.director`, `demo.director2` and `demo.admin`.

The full write-up (context, agents, hooks, spec, tests, mutation scores, lessons) is in `ASSIGNMENT.md`.

## Commits

1. Add agent context, skills, hooks and Spec Kit scaffold
2. Add Spec Kit spec, plan and tasks for approval limits
3. Add Manager and Director single-approver limits (Phase A)
4. Add Playwright rejection test and tests that kill surviving mutants (Phase A2)
5. Require two distinct approvers above 500,000 (Phase B)
6. Serialize Reject with Approve under the service lock (fix from the PR review)

## Tests

- 29 xUnit tests pass (`dotnet test backend/InvoiceApp.sln`), including both negative rules: second approver must be a Director, and the same user never counts twice.
- One Playwright test (`e2e/tests/rejected-over-limit-approval.spec.ts`): a Manager tries to approve a 50,001 invoice, sees the rejection reason, and the invoice stays PendingApproval.

## Mutation score (Stryker 4.13.0, whole `InvoiceApp.Domain` project)

```
Before (untouched code):         30.77 %  (ApprovalService.cs 60.00 %)
After  (final run):              71.26 %  (ApprovalService.cs 92.31 %)
First Phase A run:               54.10 %  (ApprovalService.cs 78.57 %)
```

The whole-project number is held down by `DiscountCalculator.cs`, which is unrelated to this feature and untested in the original repo.

## Review findings

Review runs: `test-reviewer` on the unit tests, `pr-reviewer` on `git diff main...HEAD` (two passes, the second more targeted), and my own read of the code and tests. Every finding has a verdict.

| Finding | Severity | Verdict | Reason |
| --- | --- | --- | --- |
| Boundary approval tests did not verify that approval updates DecidedAt, because the test invoice started with it already set, so a NotNull assertion would pass even if the service forgot to update it | Low | Fixed | Fixed in 9179dc3. The test invoice now starts with no decision and asserts DecidedAt falls inside the time window of the call and that DecidedBy is the approver. A fresh re-review of the test file returned no findings. |
| Playwright post-rejection assertion could read stale page content instead of the server state | Low | Fixed | Fixed in 47a6440. Found in my own review of the e2e test. The test now reloads the page after checking the error and asserts the freshly loaded invoice status. |
| DiscountCalculator.cs has 17 uncovered mutants | Info | Deferred | Unrelated to this feature and untested in the original repo. Adding tests only to raise the score would be padding. Out of scope here and noted in ASSIGNMENT.md. |
| Reject can race with Approve: Approve runs under the service lock but Reject does not, so a rejection concurrent with an approval could overwrite the transition and leave the stored status dependent on timing | Medium | Fixed | Found by the second, deeper review pass. The reviewer rated it High; I rate it Medium because the store is in-memory and single-process, but the invariant matters and the fix is small. Fixed in 6d0d391: Reject now takes the same lock as Approve, with a concurrency test that fails when the lock is removed. |
| The repository returns shared mutable invoice references, so concurrency control depends on one process-wide lock in ApprovalService | Low | Deferred | Root cause behind the race. A real data store would use optimistic concurrency (versioned updates). Out of scope for an in-memory demo repository, and the service lock is the mitigation. |
| First full-diff review by pr-reviewer returned a single No findings row | Info | Dismissed | Superseded: the second pass found the race above, so an empty result was not reliable. Nothing to fix from the first run itself. |

## How to verify

```powershell
dotnet test backend\InvoiceApp.sln
cd e2e; npm install; npx playwright install chromium; npm test
```
