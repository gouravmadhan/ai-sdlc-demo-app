# ASSIGNMENT: AI in the SDLC, Approval Limits by Role

**Repository:** `ai-sdlc-demo-app` (fork of `NITIN1670/ai-sdlc-demo-app`), branch `feature/approval-limits`
**Pull request:** https://github.com/gouravmadhan/ai-sdlc-demo-app/pull/1
**Demo video:** https://nagarro-my.sharepoint.com/:v:/p/gourav_madaan/IQBFWTxv1MNrSZcOuKUnGgbuAZOfRbCzhDIWyEdVfcIohb8?nav=eyJyZWZlcnJhbEluZm8iOnsicmVmZXJyYWxBcHAiOiJPbmVEcml2ZUZvckJ1c2luZXNzIiwicmVmZXJyYWxBcHBQbGF0Zm9ybSI6IldlYiIsInJlZmVycmFsTW9kZSI6InZpZXciLCJyZWZlcnJhbFZpZXciOiJNeUZpbGVzTGlua0NvcHkifX0&e=DvkAqJ

## 1. Overview

I took one feature from a written specification to a reviewed pull request, using an AI agent to do the work while I held the checks.

**Feature:** approval limits by role in the invoice approval service.

- A Manager may approve invoices up to 50,000.
- A Director may approve invoices up to 500,000.
- Above 500,000 an invoice needs two distinct approvers, and the second must be a Director.
- The same user may never count as both approvers on one invoice.
- A rejected attempt returns a clear reason, and nothing about the invoice changes.

| Purpose | Tool |
| --- | --- |
| AI agent | GitHub Copilot (custom agents, skills, hooks) |
| Spec-driven development | GitHub Spec Kit (`specs/001-approval-limits/`) |
| Language and unit tests | .NET 8, ASP.NET Core, xUnit |
| Mutation testing | Stryker.NET 4.13.0 |
| End-to-end test | Playwright (TypeScript) |

**How the work was split.** I used Spec Kit directly in Copilot Chat up to `/speckit-analyze`. From the implementation onward I used my own custom agents (`lead` delegating to `implementer`, plus the review agents), which is why the interaction log shows sub-agent runs only from the implementation phase.

## 2. Context: AGENTS.md

The existing `AGENTS.md` only described layout, conventions and commands. I extended it so that an agent arriving cold knows what to do and what not to do:

- **Roles.** The repo only had `Approver` and `Admin`. I introduced `Manager`, `Director` and `Admin`, retired `Approver`, and defined demo accounts `demo.manager`, `demo.director`, `demo.director2` and `demo.admin`. Admin cannot approve, so it cannot bypass the limits.
- **Approval rules** copied exactly from the brief, plus decisions that remove guesswork: limits are inclusive, "distinct" means user ID compared after trimming and ignoring case, the first approver above 500,000 may be a Manager or a Director, and the code must validate everything before it changes any state.
- **Layering.** Rules and rejection reasons live in `InvoiceApp.Domain`. Controllers only map results. No logic in `Program.cs`.
- **Tests and commands.** The required test cases, the Stryker install version, the commands, and a PowerShell note for Windows.
- **Review expectations.** Every finding gets one verdict (Fixed, Dismissed or Deferred) with a reason, in a fixed table format.

## 3. Agents, skills and hooks

### Custom agents (`.github/agents/`)

| Agent | Job |
| --- | --- |
| `lead` | Coordinates the phases and delegates. Does not edit code itself. |
| `spec-writer` | Drives the specification, plan and tasks and makes the edge cases explicit |
| `implementer` | Implements one phase at a time, tests first, and does not commit |
| `test-reviewer` | Read-only audit of unit tests for weak assertions and missing boundaries |
| `pr-reviewer` | Read-only review of the diff against the rules, outputs findings in the fixed format |

### Skills (`.github/skills/`)

- `approval-rules`: the rules, a test matrix and common agent mistakes.
- `review-findings-format`: the `Finding | Severity | Verdict | Reason` table.
- `mutation-testing`: how to run Stryker, record scores and kill survivors.

### Hooks and logging

- `.github/hooks/ai-interaction-log.json` runs `scripts/hooks/log-interaction.ps1` on session start, prompt, tool use, sub-agent stop and session end.
- The script appends one JSON line per event to `logs/ai-interactions.jsonl`, always with the same fields (timestamp, event, session ID, agent, prompt, tool, tool input, result summary, reason). It never blocks the agent.
- `scripts/validate-review.ps1` checks that review output uses the fixed table format, that each verdict is Fixed, Dismissed or Deferred, and that every reason is non-empty.

### What the log shows

`logs/ai-interactions.jsonl` is the record of the AI work. At the time of the final review it held **533 entries across 27 sessions**, from 2026-10-31 to 2026-11-01:

| Event | Count |
| --- | --- |
| Prompts submitted | 29 |
| Tool calls | 485 |
| Sub-agent stops | 16 |
| Session start / end | 2 / 1 |

Skills were loaded 13 times in the logged work (`approval-rules` 7, `review-findings-format` 4, `speckit-tasks` 1, `mutation-testing` 1). The `lead` session delegated to the `implementer` agent four times: Phase A backend, the Playwright rejection flow, a follow-up that made the Playwright test check freshly loaded invoice status, and the Phase B approval history. The log starts when the plan was being generated, so the Spec Kit constitution, specify and clarify steps are not in it. `/speckit-plan`, `/speckit-tasks` and `/speckit-analyze` are.

## 4. Specification with Spec Kit

Spec Kit generated the constitution, spec, clarifications, plan, research, data model, contracts, quickstart and tasks in `specs/001-approval-limits/`. I ran the plan with this input: *"Use the existing stack of the repo. Keep rules in the service layer. Unit tests with xUnit, Stryker for mutation testing, one Playwright E2E test for a rejected over-limit approval."* Then I ran tasks and `/speckit-analyze`.

I edited `tasks.md` after the analysis because I found problems in it:

1. **A gap in Phase A.** The approval history only arrives in Phase B, so Phase A did not say what happens above 500,000. I added an interim rule: any invoice above 500,000 is rejected with `Invoices above 500,000 require two approvers`. Phase B replaced it.
2. **No "before" mutation score** in the tasks. I added a baseline task that runs before any code change.
3. **One large Phase A commit.** I split it into a backend commit and a Playwright commit.
4. **Thin negative-rule tests.** I listed every required test by name, including the same Director approving twice and two different Managers.
5. **No delivery phase.** I added one for the score comparison, the review and the write-up.

The tasks are split into two implementation phases: Phase A (single-approver limits) and Phase B (the two-approver rule).

## 5. Implementation steps

| Step | What and why |
| --- | --- |
| Agent context, skills, hooks | `AGENTS.md`, agents, skills, logging hook, Spec Kit scaffold |
| Spec, plan and tasks | Spec Kit output and the pre-change mutation baseline |
| Manager and Director limits (Phase A) | Roles, inclusive limits, Admin rejected, interim rule above 500,000, `README` demo accounts updated |
| Playwright test and mutant-killing tests (Phase A2) | UI-level proof of a rejected over-limit approval, plus tests added after Stryker survivors |
| Two distinct approvers above 500,000 (Phase B) | Approval history, second approver must be a Director, same user rejected, all checks before any state change |
| Serialize Reject with Approve (review fix) | A deeper review pass found that `Reject` did not take the service lock that `Approve` holds; both now run under the same lock, with a concurrency test |

Each step was a separate prompt to the agent with an explicit task range ("T002 to T004 only", "do not start Phase B"), reviewed and committed by me. The six steps above are the six commits in the pull request.

## 6. Tests

### Rule-to-test traceability (xUnit, `ApprovalServiceTests`)

| Rule | Tests |
| --- | --- |
| Manager up to 50,000 | `Manager_At50000_IsApproved`, `Manager_Above50000_IsRejectedWithoutChangingInvoice` |
| Director up to 500,000 | `Director_At500000_IsApproved` |
| Above 500,000, first approval stays pending | `Manager_FirstApproval_Above500k_LeavesInvoicePendingWithOneApproval`, `Director_FirstApproval_Above500k_LeavesInvoicePendingWithOneApproval` |
| Above 500,000, two distinct approvers complete it | `TwoDistinctDirectors_Above500k_CompleteApproval`, `Manager_FirstThenDirectorSecond_Above500k_CompletesApproval` |
| **Negative rule 1:** second approver must be a Director | `DirectorThenManagerAsSecond_Above500k_IsRejected`, `TwoDifferentManagers_Above500k_SecondIsRejected` |
| **Negative rule 2:** same user never counts twice | `SameDirectorTwice_Above500k_IsRejected`, `SameManagerTwice_Above500k_IsRejected`, `SameUserIdWithDifferentCasingOrWhitespace_IsRejected` |
| Only Manager and Director can approve | `NonApproverRole_IsRejectedWithoutChangingInvoice` (Admin, Unknown) |
| Invoice must be pending | `InvoiceNotPending_IsRejectedWithoutChangingInvoice` (Draft, Approved, Rejected), `ThirdApproval_AfterCompletion_IsRejectedAsWrongStatus` |
| Approval record keeps a trimmed ID and UTC time | `InvoiceApproval_StoresTrimmedApproverIdAndUtcApprovalTime` |
| Concurrent attempts record one approval | `ConcurrentApprovals_BySameUser_RecordOnlyOneApproval` |
| Reject and Approve cannot both succeed on one invoice | `Reject_ConcurrentWithApprove_ExactlyOneDecisionSucceeds` |

The solution runs **29 tests, all passing** (it was 14 at the first Phase A Stryker run). The rejection tests check the specific reason and compare the invoice before and after the attempt, so a rejected approval that quietly changed something would fail.

### Playwright

One end-to-end test, `e2e/tests/rejected-over-limit-approval.spec.ts`: a Manager signs in, tries to approve a 50,001 invoice, sees the Manager-limit rejection reason, and the invoice is still `PendingApproval`. It uses page objects and role-based locators.

## 7. Mutation testing (Stryker 4.13.0)

Stryker mutates the whole `InvoiceApp.Domain` project. That includes `DiscountCalculator.cs`, which is unrelated to this feature and has no tests in the original repo, so it pulls the whole-project number down.

```
Before (untouched code, whole Domain project):  30.77 %
After  (whole Domain project, final run):       71.26 %
```

The first Phase A run, taken before I strengthened the tests, is a useful middle point:

```
Phase A, first run (whole Domain project): 54.10 %   (78 mutants: 33 killed, 9 survived, 19 no coverage, 17 ignored)
    ApprovalService.cs:    78.57 %  (82.50 % of covered mutants; 7 survived, 2 no coverage)
    DiscountCalculator.cs:  0.00 %  (17 no coverage, unrelated to this feature)
    Invoice.cs:             0.00 %  (2 survived)
```

Reading the per-file table mattered. The 54.10% headline looked poor, but 17 of the 19 uncovered mutants sat in `DiscountCalculator.cs`. The feature's own code, `ApprovalService.cs`, was at 78.57%, so I put the effort there. I added and strengthened tests for boundaries, role rejection and exact rejection reasons, and I did not write tests for unrelated code just to raise the number.

## 8. Pull request review

I reviewed my own work with the review agents and judged every result myself. The full-diff review used `git diff main...HEAD` against `AGENTS.md` and the `approval-rules` skill, and saved its output in the fixed table format. That first pass returned "No findings". A second, more targeted pass found a real problem the first one missed (the `Reject` race below).

| Finding | Severity | Verdict | Reason |
| --- | --- | --- | --- |
| Boundary approval tests did not verify that approval updates `DecidedAt`, because the test invoice started with it already set, so `Assert.NotNull` would pass even if the service forgot to update it | Low | Fixed | The test invoice now starts with no decision and asserts that `DecidedAt` falls inside the time window of the call and that `DecidedBy` is the approver. A fresh re-review of the test file then returned no findings. |
| Playwright post-rejection assertion could read stale page content instead of the server state | Low | Fixed | Found in my own review of the e2e test. The test now reloads the page after checking the error and asserts the freshly loaded invoice status. |
| `DiscountCalculator.cs` has 17 uncovered mutants | Info | Deferred | Unrelated to this feature and untested in the original repo. Adding tests only to raise the score would be padding, so it is out of scope here. |
| `Reject` can race with `Approve`: `Approve` runs under the service lock but `Reject` does not, so a rejection concurrent with an approval could overwrite the transition and leave the stored status dependent on timing | Medium | Fixed | Found by the second, deeper review pass (rated High by the reviewer; I rate it Medium because the store is in-memory and single-process, but the invariant matters and the fix is small). Fixed in 6d0d391: `Reject` now takes the same lock as `Approve`, with a concurrency test that fails when the lock is removed. |
| The repository returns shared mutable invoice references, so concurrency control depends on one process-wide lock in `ApprovalService` | Low | Deferred | This is the root cause behind the race. A real data store would use optimistic concurrency (versioned updates). That is out of scope for an in-memory demo repository, and the service lock is the mitigation. |
| First full-diff review (`pr-reviewer`) returned a single "No findings" row | Info | Dismissed | Superseded: the second pass found the race above, so an empty result was not reliable. Nothing to fix from the first run itself. |

## 9. What went wrong and what I learned

- **Sub-agents cannot always run commands.** My `lead` agent tried to delegate `dotnet test` to sub-agents, and every one of those attempts (twelve in the log) came back saying no command could be run because the sub-agent depth limit had been reached. Nothing was verified that way. I ran the tests myself in the terminal, which is where the passing test results come from. Delegation is not verification.
- **Runtime mismatch.** `dotnet test` first failed because the project targets .NET 8 and only the .NET 10 runtime was installed. I installed the .NET 8 SDK alongside it. Stryker 4.13.0 also needs the .NET 8 line.
- **A weak test that looked fine.** The reviewer caught a boundary test whose `DecidedAt` assertion could pass even if the service did nothing. A green run does not mean the tests mean something, which is why mutation testing is part of this assignment.
- **The headline mutation score misled at first.** Most of the uncovered mutants were in an unrelated file. The per-file report changed what I had to do.
- **Spec gaps.** Spec Kit's tasks left Phase A without a rule for invoices above 500,000. Running the analysis was not enough. I had to read the tasks against my own rules.
- **An empty review is not proof, and it was wrong.** The first agent review of the full diff reported no findings. A second, more targeted pass found that `Reject` was not serialized with `Approve`. I had added the lock for approvals only, and never thought about rejections. Holding the checks means asking the reviewer specific questions, not accepting one agent's "all clear".
- **Shell differences.** `tee` does not exist in Windows Command Prompt, so I used PowerShell's `Tee-Object`.

## 10. Deliverables map

| Deliverable | Where |
| --- | --- |
| Pull request with standalone commits | https://github.com/gouravmadhan/ai-sdlc-demo-app/pull/1 (six commits, listed in section 5) |
| Updated `AGENTS.md` | Repository root |
| Specification files | `specs/001-approval-limits/`, `.specify/` |
| Unit tests for every rule, including both negative rules | `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs` (section 6) |
| Playwright test for a rejected over-limit approval | `e2e/tests/rejected-over-limit-approval.spec.ts` |
| Mutation scores before and after | Section 7, with the raw output in `mutation-before.txt` and `mutation-after.txt` |
| Review findings with verdicts | Section 8 and the pull request description |
| AI interaction log | `logs/ai-interactions.jsonl` (section 3) |
| This write-up and the video link | `ASSIGNMENT.md` |
