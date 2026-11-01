# Project conventions for AI agents

Invoice approval demo app: ASP.NET Core 8 Web API with a small static web UI.

## Layout

- `backend/src/InvoiceApp.Domain`: business rules, no web or storage code. `ApprovalService` owns the invoice lifecycle (Draft, PendingApproval, Approved, Rejected).
- `backend/src/InvoiceApp.Api`: controllers, sign-in (`Auth/`), in-memory repository, and the web UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit tests, one test class per class under test, named `<ClassUnderTest>Tests`.
- `e2e`: Playwright end-to-end tests (`tests/`) and page objects (`pages/`).
- `docs/use-cases`: short briefs for features we may build.
- `specs/` and `.specify/`: Spec Kit output (spec, plan, tasks, constitution). Read the active spec before changing approval behaviour.
- `logs/ai-interactions.jsonl`: AI interaction log, written by hooks. Never edit it by hand.

## Conventions

- Controllers live in `Controllers/`, one per resource, route `api/[controller]`. Request bodies are `record` types declared inside the controller.
- Controllers inherit `ApiControllerBase` and use `Handle(...)` so service errors map to 404, 409 and 400.
- Every endpoint is behind sign-in. Restrict actions with `[RequireRole("...")]`: destructive actions are Admin only, approvals are Manager or Director. The amount each role may approve is defined in "Approval rules" below and enforced in Domain.
- Business rules go in the Domain project, never in controllers.
- Register services in `Program.cs`. Repositories are interfaces in Domain with an in-memory implementation in Api.
- Every new rule or endpoint needs tests. Use `FakeInvoiceRepository` in tests.
- Use clear names; no one-letter variables or `flag` parameters.
- End-to-end tests use page objects and role-based locators (`getByRole`, `getByLabel`), never CSS or XPath selectors.

## Roles

The system has three roles. The old `Approver` role is retired and replaced by `Manager` and `Director`.

| Role | Can approve? | Approval limit | Other permissions |
| --- | --- | --- | --- |
| `Manager` | Yes | Up to 50,000 | Create, submit, approve or reject invoices within the limit |
| `Director` | Yes | Up to 500,000 alone; also valid as the required second approver above 500,000 | Everything a Manager can do |
| `Admin` | No | None | Destructive actions only (for example delete). An Admin can never approve, so Admin cannot bypass the limits. |

- Role names are the exact strings `Manager`, `Director` and `Admin`, used in `[RequireRole("...")]` and in the sign-in role claim.
- Approval endpoints use `[RequireRole("Manager", "Director")]`. This only decides who may call the endpoint. The amount limits are enforced in Domain, never in the attribute.
- Remove `Approver` everywhere it appears in code, UI text, tests and e2e tests, and update anything that used `demo.approver`.

## Approval rules (authoritative)

These are the business rules for approving an invoice. If code, tests or a spec disagree with this section, stop and flag it instead of guessing.

1. A **Manager** may approve an invoice with an amount up to **50,000**.
2. A **Director** may approve an invoice with an amount up to **500,000**.
3. An invoice above **500,000** needs **two distinct approvers**, and the **second approver must be a Director**.
4. The **same user may never count as both approvers** on one invoice.
5. A **rejected attempt returns a clear reason, and nothing about the invoice changes** (status, approver list, amount, timestamps: all identical to before the attempt).

### Decisions (so nobody has to guess)

- Limits are **inclusive**: exactly 50,000 is allowed for a Manager, exactly 500,000 is allowed for a single Director.
  - Manager: 50,000 passes, 50,001 is rejected.
  - Director alone: 500,000 passes, 500,001 needs a second approver.
- "Distinct" means distinct **user ID**, compared case-insensitively after trimming whitespace. Never compare user objects or display names.
- The first approver on an invoice above 500,000 may be a Manager or a Director. The **second** must be a Director. (Confirm in the spec if this is changed.)
- Only `Manager` and `Director` can approve. `Admin` and any other role are rejected with a reason, never silently ignored.
- An invoice must be in `PendingApproval` to be approved. Approving an invoice in any other state is rejected with a reason.
- **Validate everything first, then change state.** Never record the first approver, change status or write to the repository before every rule has passed. A rejection must leave the stored invoice exactly as it was.
- Rejections use a typed result or domain exception with a stable reason, not a bare `false` and not a generic message. The controller maps it through `Handle(...)`. Reasons must say what rule failed, for example: `Manager limit is 50,000`, `Second approver must be a Director`, `Same user cannot approve twice`.

## Layering

- **Domain (`ApprovalService` and domain types)**: owns every approval rule above, the role limits and the rejection reasons. No HTTP, no storage, no `ClaimsPrincipal`.
- **Api (controllers, `Auth/`)**: authenticates, passes the user ID and role to the service, and maps the result to an HTTP response. No limit checks, no amount comparisons, no "is this a Director" logic here.
- **UI (`wwwroot/`)**: shows the reason returned by the API. It does not decide anything.
- Limits (50,000 and 500,000) are named constants or configuration in Domain, not magic numbers scattered through the code.
- Do not move code between layers or refactor unrelated files in the same commit as a rule change.

## Tests

- Unit tests live in `backend/tests/InvoiceApp.Tests`, named `ApprovalServiceTests`. One behaviour per test; names say the rule, for example `Manager_CannotApprove_AboveLimit`.
- Every approval rule needs a test, and every rejection test must **assert the invoice is unchanged** (compare the stored invoice before and after), not just the returned reason.
- Required cases:
  - Manager at 50,000 passes; Manager at 50,001 is rejected.
  - Director at 500,000 passes with one approval; Director at 500,001 stays pending until a second approver.
  - Above 500,000: Director plus a different Director passes.
  - Above 500,000: second approver is a Manager, rejected, invoice unchanged.
  - Above 500,000: same user twice, rejected, including different casing or whitespace in the ID.
  - Admin (and any non-approver role) is rejected, invoice unchanged.
  - Invoice not in `PendingApproval` is rejected.
- Do not write tests that only assert `NotNull`, `True` or "no exception". A test that cannot fail on a wrong boundary (`<` vs `<=`) is not a test.
- Mutation testing with Stryker is the check on test quality. Surviving mutants on the approval rules must be killed or explained.
- One Playwright test covers a rejected over-limit approval through the UI: sign in as a Manager, try to approve an invoice above 50,000, assert the visible rejection reason and that the invoice status has not changed.

## Commands

- Run the app: `dotnet run --project backend/src/InvoiceApp.Api` then open http://localhost:5000
- Run the tests: `dotnet test backend/InvoiceApp.sln`
- Run end-to-end tests: `cd e2e && npm install && npx playwright install chromium && npm test`
- Install mutation testing (needs this exact version on .NET 8): `dotnet tool install -g dotnet-stryker --version 4.13.0`
- Run mutation testing: from `backend/tests/InvoiceApp.Tests` run `dotnet stryker`. Reports go to `StrykerOutput/` (git-ignored).
- Shell note: on Windows use PowerShell (`Tee-Object` instead of `tee`).

## Workflow

- Specification first, using Spec Kit: `/speckit.specify`, `/speckit.clarify`, `/speckit.plan`, `/speckit.tasks`, `/speckit.analyze`. Commit the generated files.
- Implement in **at least two separate steps**, each its own commit with a message that says what changed and why:
  1. Single-approver limits (Manager and Director) with tests.
  2. Two-approver rule above 500,000 with tests, including both negative rules.
- Run `dotnet test backend/InvoiceApp.sln` before every commit. Do not commit on red.
- Never ask for the whole feature in one prompt. Keep each change small enough to review in one sitting.

## Agents, skills and hooks

- Custom agents (`.github/agents/`): `lead` (start here, coordinates), `spec-writer`, `implementer`, `test-reviewer`, `pr-reviewer`. The lead delegates; sub-agents do one job each.
- Skills (`.github/skills/`): `approval-rules`, `review-findings-format`, `mutation-testing`.
- Hooks (`.github/hooks/ai-interaction-log.json`) run `scripts/hooks/log-interaction.ps1`, which appends one JSON line with the same fields for every event to `logs/ai-interactions.jsonl`.
- Check review output with `scripts/validate-review.ps1` (see the `review-findings-format` skill).

## Review expectations

- Review every generated test by reading it. Delete or fix weak ones.
- Review the diff against the approval rules above. The two highest-risk areas are:
  - state being changed before validation passes (partial state on rejection);
  - the "same user" and "second must be a Director" rules.
- Every review finding gets exactly one verdict, with a reason:
  - **Fixed**: say which commit fixed it.
  - **Dismissed**: say why it is not a problem.
  - **Deferred**: say why it is out of scope now and where it is tracked.
- Report findings in this format: `Finding | Severity | Verdict | Reason`.
- Do not mark every finding as fixed. Use judgement.

## Demo accounts (not real credentials)

- `demo.manager` / `ChangeMe123!` (role Manager)
- `demo.director` / `ChangeMe123!` (role Director)
- `demo.director2` / `ChangeMe123!` (role Director). A second, distinct Director, needed to test the two-approver rule above 500,000.
- `demo.admin` / `ChangeMe123!` (role Admin)
- `demo.approver` is removed. It is replaced by `demo.manager`.
- `demo.manager`, `demo.director` and `demo.director2` are added to the sign-in code in `Auth/` as part of this feature. The three approver accounts must have different user IDs.

## Never

- Commit real credentials, tokens or customer data.
- Add business logic to `Program.cs` or controllers.
- Compare approvers by display name or object reference.
- Change invoice state before all approval rules have passed.
- Edit or delete entries in `logs/ai-interactions.jsonl`.
