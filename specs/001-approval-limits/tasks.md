# Tasks: Approval Limits by Role

**Input**: Design documents from `specs/001-approval-limits/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`contracts/approvals.md`, and `quickstart.md`

**Organization**: Implementation is split into two sequential phases as requested:
Phase A implements single-approver role limits; Phase B adds the high-value
two-approver rule. Phase 0 captures the mutation baseline before any code changes and
Phase C closes out the delivery evidence. Each phase has its own validation and commit
gate.

**Revision note**: This file was revised after `/speckit-analyze` to (1) define the
interim Phase A behaviour above 500,000, (2) add a mutation-testing baseline before any
change, (3) split Phase A into a backend commit and a Playwright commit, and (4) add a
delivery phase. Task IDs were renumbered.

**Agents**: Run implementation tasks through the `lead` agent, which delegates to
`implementer`; run test audits with `test-reviewer`; run the pull request review with
`pr-reviewer`. Do not request a whole phase in one prompt beyond the task ranges below.

## Phase 0: Baseline (before any code change)

**Goal**: Record the mutation score of the untouched code so the "before" number is
honest.

- [ ] T001 Run `dotnet test backend/InvoiceApp.sln`, then run `dotnet stryker` from `backend/tests/InvoiceApp.Tests` (install `dotnet-stryker` 4.13.0 if unavailable) and save the console output to `mutation-before.txt` at the repository root. Record the line `The final mutation score is NN.NN %`. Add `StrykerOutput/` to `.gitignore`. Make no source changes in this task.

**Phase 0 checkpoint**: `mutation-before.txt` exists and contains the baseline score.

## Phase A: Single-Approver Limits (Manager and Director)

**Goal**: Only Manager and Director roles can approve; Managers can approve up to
50,000 inclusive and Directors up to 500,000 inclusive. A rejected approval gives a
specific reason and leaves the invoice unchanged. Retire the legacy `Approver` role.

**Interim Phase A behaviour above 500,000**: The approval-history model arrives in
Phase B, so until then any invoice above 500,000 is rejected for every role with the
reason `Invoices above 500,000 require two approvers`, and the invoice is left
unchanged. No single approver can complete a high-value invoice at any point in the
history. Phase B replaces this rule with the first-approval-stays-pending flow and
updates its test.

**Independent Test**: Run the xUnit suite and verify Manager approval at 50,000,
rejection at 50,001 with unchanged invoice state, Director approval at 500,000, the
interim rejection above 500,000, and (in the second Phase A commit) a single Playwright
scenario where a Manager sees the over-limit reason and the invoice remains pending.

### A1: Backend limits

- [x] T002 [US1] Add xUnit tests in `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs` for: Manager at 50,000 approved; Manager at 50,001 rejected; Director at 500,000 approved; Director at 500,001 rejected with the interim reason `Invoices above 500,000 require two approvers`; Admin and unknown roles rejected; non-`PendingApproval` invoices rejected. Remove legacy `demo.approver` references. Every rejected-attempt test must compare all invoice fields before and after, not only the returned reason. Agent: `implementer`.
- [x] T003 [US1] Implement role-aware single-approver validation in `backend/src/InvoiceApp.Domain/ApprovalService.cs`: inclusive limits as named constants, the interim above-500,000 rejection, stable rule-specific rejection reasons, and validate-before-mutate behaviour. Accept user ID and role as service inputs. Agent: `implementer`.
- [x] T004 [US1] Restrict the approval controller to Manager and Director and pass the authenticated user ID and role to the service in `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`; replace the legacy demo approver with `demo.manager`, `demo.director`, and `demo.director2` in `backend/src/InvoiceApp.Api/appsettings.json`; update the seeded historical approver ID in `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`. No amount or role-limit logic in the controller. Agent: `implementer`.
- [ ] T005 Run `dotnet test backend/InvoiceApp.sln`. Run the `test-reviewer` agent on `ApprovalServiceTests.cs`, then fix valid findings and note dismissed ones for the pull request. Search for leftover `Approver` and `demo.approver` references. Stage and commit `backend/src/InvoiceApp.Domain/ApprovalService.cs`, `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, `backend/src/InvoiceApp.Api/appsettings.json`, `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`, and `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs` with a message explaining the role-specific limits and role migration.

**Checkpoint A1**: Single-approver boundaries, role authorization, the interim
above-500,000 rejection, and rejection atomicity pass in unit tests. Commit before
starting A2.

### A2: Playwright rejection test

- [x] T006 [P] [US3] Add Playwright configuration and reusable sign-in/invoice page objects using accessible role and label locators in `e2e/playwright.config.ts` and `e2e/pages/invoice-approval.page.ts`. Reuse existing page objects where they exist. Agent: `implementer`.
- [x] T007 [US3] Add exactly one Playwright test that creates and submits a 50,001 invoice, signs in as `demo.manager`, attempts approval, and asserts the visible Manager-limit reason and unchanged PendingApproval status in `e2e/tests/rejected-over-limit-approval.spec.ts`. If the UI in `backend/src/InvoiceApp.Api/wwwroot/` does not display the API `error` message on rejection, make the smallest change needed to show it. Agent: `implementer`.
- [ ] T008 Run `dotnet test backend/InvoiceApp.sln` and `npm test` from `e2e` (use `npx playwright test --headed` once to watch it). Run `dotnet stryker` from `backend/tests/InvoiceApp.Tests`; kill or explain surviving approval-rule mutants and commit any added tests. Stage and commit `e2e/playwright.config.ts`, `e2e/pages/invoice-approval.page.ts`, `e2e/tests/rejected-over-limit-approval.spec.ts`, and any `wwwroot` change with a message explaining that the Manager rejection is verified through the UI.

**Phase A checkpoint**: Single-approver boundaries, role authorization, rejection
atomicity, and the Manager UI rejection scenario pass. Both Phase A commits are made
before starting Phase B.

## Phase B: Two-Approver Rule Above 500,000

**Goal**: An invoice above 500,000 stays pending after its first Manager or Director
approval and becomes approved only after a distinct Director's second approval.
Rejections and concurrent attempts must preserve the approval rules and invoice state.
This phase replaces the Phase A interim above-500,000 rejection.

**Independent Test**: Run the expanded xUnit suite to verify a valid distinct Director
second approval, rejection of a Manager second approver, normalized same-user rejection,
unchanged invoice data on every rejection, serialized concurrent approvals, and the named negative-rule tests in T010 (second approver must be a Director; same user never counts twice).

- [x] T009 [US2] Add an immutable approval-history record with user ID, role, and UTC approval time in `backend/src/InvoiceApp.Domain/InvoiceApproval.cs`, and add an ordered `Approvals` collection to `backend/src/InvoiceApp.Domain/Invoice.cs`; store user IDs trimmed of surrounding whitespace and compare them case-insensitively. Agent: `implementer`.
- [x] T010 [US2] Add xUnit tests in `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs`. Required cases, each with a test name that states the rule:
  - `Manager_FirstApproval_Above500k_LeavesInvoicePendingWithOneApproval`
  - `Director_FirstApproval_Above500k_LeavesInvoicePendingWithOneApproval`
  - `TwoDistinctDirectors_Above500k_CompleteApproval`
  - `Manager_FirstThenDirectorSecond_Above500k_CompletesApproval`
  - NEGATIVE RULE 1 (second approver must be a Director): `DirectorThenManagerAsSecond_Above500k_IsRejected` and `TwoDifferentManagers_Above500k_SecondIsRejected`. Each asserts the specific rejection reason, that the invoice is still PendingApproval with exactly one recorded approval, and that every other field is unchanged.
  - NEGATIVE RULE 2 (same user never counts twice): `SameDirectorTwice_Above500k_IsRejected`, `SameManagerTwice_Above500k_IsRejected`, and `SameUserIdWithDifferentCasingOrWhitespace_IsRejected`. Each asserts the specific rejection reason and complete invoice equality before and after the attempt.
  - `ThirdApproval_AfterCompletion_IsRejectedAsWrongStatus`
  - `ConcurrentApprovals_BySameUser_RecordOnlyOneApproval` (see the scope note).
  Update the Phase A interim test so a first approval above 500,000 now succeeds and leaves the invoice pending. Every rejection test compares the whole invoice, including recorded approvals, before and after, and asserts the rejection reason text. Agent: `implementer`.
- [x] T011 [US2] Extend `backend/src/InvoiceApp.Domain/ApprovalService.cs` to remove the interim rejection, accept a Manager or Director as the first approver above 500,000, require a distinct Director second, preserve `DecidedBy` and `DecidedAt` as the latest-approval compatibility fields, validate all rules before creating and persisting a replacement invoice, and serialize the complete approval read/validate/update operation. Agent: `implementer`.
- [x] T012 [P] [US2] Register `ApprovalService` as a singleton so its approval serialization applies across API request scopes in `backend/src/InvoiceApp.Api/Program.cs`, and make `FakeInvoiceRepository.Update` replace the stored invoice instance so copy-on-success behavior is testable in `backend/tests/InvoiceApp.Tests/FakeInvoiceRepository.cs`.
- [ ] T013 Run `dotnet test backend/InvoiceApp.sln` and `dotnet test backend/InvoiceApp.sln --list-tests`; confirm every test named in T002 and T010 exists and keep the list for the rule-to-test table in `ASSIGNMENT.md`. In a scratch change (then revert it) save the first approval before the second-approver check, and replace the same-user comparison with a plain `==`; at least one test must fail each time. Run the `test-reviewer` agent again. Check specifically that no approval is persisted before the second-approver and same-user checks pass, and that users are compared by normalised ID. Run `dotnet stryker` from `backend/tests/InvoiceApp.Tests`; kill or explain surviving approval-rule mutants, then run `npm test` from `e2e`. Stage and commit `backend/src/InvoiceApp.Domain/ApprovalService.cs`, `backend/src/InvoiceApp.Domain/InvoiceApproval.cs`, `backend/src/InvoiceApp.Domain/Invoice.cs`, `backend/src/InvoiceApp.Api/Program.cs`, and `backend/tests/InvoiceApp.Tests/` with a message explaining the two-distinct-approver requirement.

**Phase B checkpoint**: Every requested approval rule passes its unit tests, rejected
attempts leave all invoice fields and accepted approvals unchanged, and the single
Playwright rejection test still passes.

**Scope note**: Concurrency handling (FR-013, T012 and the concurrent-attempt test in
T010) goes beyond the original brief. If the concurrency test proves flaky or the lock
mutant cannot be killed, defer FR-013, keep the single-threaded rules, and record the
deferral and its reason in the pull request description.

## Phase C: Delivery Evidence

**Goal**: Produce the remaining assignment evidence after both phases are committed.

- [ ] T014 Run `dotnet stryker` from `backend/tests/InvoiceApp.Tests` and save the output to `mutation-after.txt`. Record the `The final mutation score is NN.NN %` line next to the baseline from T001. Add tests for any surviving approval-rule mutants and commit them.
- [ ] T015 Push the branch. Run the `pr-reviewer` agent on `git diff main...HEAD`, save its output as `review-findings.md`, and validate it with `scripts/validate-review.ps1 -Path review-findings.md -AllowPending`. Judge every finding as Fixed (with commit), Dismissed (with reason), or Deferred (with reason); at least one finding should be dismissed or deferred on its merits. Commit any fixes. Write `pr-description.md` with the final table, validate it with `scripts/validate-review.ps1 -Path pr-description.md`, and open the pull request on the fork with the fork's `main` as the base.
- [ ] T016 Check `logs/ai-interactions.jsonl` contains prompts, tool calls, and sub-agent stops from this work. Note in `ASSIGNMENT.md` that the Spec Kit commands used before the custom agents existed were run directly and list their prompts. Commit the log.
- [ ] T017 Write `ASSIGNMENT.md` at the repository root: overview and tool choices, how context/skills/hooks/agents were used, the with-versus-without context comparison, Spec Kit artifacts, mutation scores before and after as plain text, review findings and verdicts, lessons learned, and the demo video link. Record the 8 to 10 minute demo with the unit tests, Stryker, and the Playwright test run live.

**Phase C checkpoint**: Pull request open with at least three standalone commits,
scores recorded as text, every finding judged, log committed, write-up and video linked.

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 0**: Runs on the untouched code, before Phase A. Commit with the spec files.
- **Phase A**: Starts after Phase 0. A1 (T002-T005) is committed before A2 (T006-T008).
- **Phase B**: Depends on both Phase A commits because it extends the role-aware service
  and replaces the interim above-500,000 rule.
- **Phase C**: Depends on Phase B.
- **Commit gates**: Each gate's full solution tests must pass before its commit. Run
  Stryker for each phase; do not commit while approval-rule mutants remain unexplained.

### Task Dependencies

- T001 precedes all code changes.
- T002 precedes T003; T003 precedes T004; T005 depends on T002-T004 and closes A1.
- T006 can run in parallel with T002-T004 because it is limited to Playwright setup and
  page objects in separate files, but it is committed in A2, after T005.
- T007 depends on T004 and T006; T008 depends on T006-T007 and closes Phase A.
- T009 precedes T010 so approval-history types are available to the tests.
- T010 precedes T011. T011 implements the tested business flow; T012 can run in
  parallel with T011 because it changes dependency registration and the test repository.
- T013 depends on T009-T012 and closes Phase B.
- T014 depends on T013; T015 depends on T014; T016 and T017 depend on T015.

### Parallel Opportunities

- **Phase A**: T006 may run in parallel with T002-T004. T002/T003/T004 are otherwise
  sequential because the service signature and API call depend on the preceding work.
- **Phase B**: T012 can run in parallel with T011 because the API registration and fake
  repository update are in separate files from the service's rule implementation.
- The phases themselves are not parallel: each extends the committed behaviour of the
  one before.

## Implementation Strategy

1. Capture the mutation baseline (T001), then commit it together with the Spec Kit files.
2. Complete A1 and commit the single-approver behaviour as a reviewable increment.
3. Complete A2 and commit the Playwright rejection test separately.
4. Complete Phase B and commit the distinct second-approver behaviour as its own
   increment.
5. Keep the existing API response compatibility fields while adding ordered approval
   history; do not move business decisions into controllers or UI code.
6. Use the xUnit suite and Stryker to verify domain rules, and the single Playwright
   scenario to verify the user-visible Manager rejection.
7. Close with the mutation after-score, the agent-assisted pull request review, the
   interaction log, and `ASSIGNMENT.md`.
