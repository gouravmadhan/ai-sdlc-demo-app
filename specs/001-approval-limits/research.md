# Research: Approval Limits by Role

## Repository Findings

- The application targets .NET 8 and separates `InvoiceApp.Domain` from
  `InvoiceApp.Api`.
- `ApprovalService` currently owns invoice state transitions. It receives
  `IInvoiceRepository`; the API registers the repository as a singleton and the
  service as scoped.
- The in-memory repository protects individual operations with a lock, but
  `GetById` returns a mutable invoice reference. Separate locked reads and writes
  therefore do not serialize a complete read/validate/update approval decision.
- `Invoice` currently stores one `DecidedBy` and `DecidedAt`, which cannot represent
  two distinct approvals.
- The approval route is `POST /api/approvals/{invoiceId}/approve`. The controller
  currently reads the authenticated username but does not pass its role to the domain
  service. `ApiControllerBase.Handle(...)` maps `InvalidOperationException` to HTTP
  409 with an `error` field.
- The static UI already renders API error text for failed decisions. The xUnit project
  uses `FakeInvoiceRepository`; the E2E project uses Playwright Test.
- No OpenAPI contract or existing E2E page objects are present.

## Decisions

### Keep all approval rules in the Domain service

- **Decision**: The service accepts the user ID and role and evaluates role eligibility,
  limits, approval sequence, invoice state, and normalized identity.
- **Rationale**: This preserves the existing domain/API boundary and allows all rules,
  including Admin rejection, to be unit-tested without HTTP.
- **Alternatives considered**: Enforcing amount limits in the controller or UI was
  rejected because it duplicates rules and cannot protect non-UI callers.

### Replace one-approver fields with approval history while preserving existing fields

- **Decision**: Add an approval-history collection to the invoice and a domain approval
  record containing user ID, role, and UTC decision time. Keep `DecidedBy` and
  `DecidedAt` synchronized to the latest accepted approval for existing response
  consumers.
- **Rationale**: Two distinct approvers must be recorded and checked; a single scalar
  approver field cannot establish the history or identity rule.
- **Alternatives considered**: Storing two positional approver fields was rejected
  because it hard-codes the sequence into the invoice shape and is less extensible.

### Validate and persist an updated copy

- **Decision**: Evaluate every applicable rule using the current invoice and create a
  replacement invoice only after validation succeeds. Rejections return a stable
  rule-specific domain error without changing the existing object or calling repository
  update.
- **Rationale**: The repository exposes mutable objects, so copying and replacing avoids
  partial field changes when validation rejects an attempt.
- **Alternatives considered**: Mutating the fetched object and rolling it back on error
  was rejected because rollback is fragile and can miss fields.

### Serialize concurrent approval attempts in this single-process demo

- **Decision**: Register `ApprovalService` as a singleton and protect the complete
  approval read/validate/replace operation with one service-owned lock. The in-memory
  repository is already a singleton. Each overlapping approval therefore observes the
  invoice state left by the preceding accepted attempt.
- **Rationale**: This establishes a definite order across HTTP request scopes without
  introducing a storage engine or a distributed coordination dependency. The existing
  demo is single-process and has no concurrency throughput target.
- **Alternatives considered**: Locking only repository `GetById` and `Update` calls was
  rejected because it leaves a race between validation and update. Per-invoice lock
  registries were rejected for this demo because their lifecycle and cleanup add
  unnecessary complexity. Cross-process coordination is out of scope with in-memory
  storage.

### Retire the legacy role and use the existing authentication session

- **Decision**: Permit `Manager` and `Director` for approval; remove legacy `Approver`
  role usage. Pass the session username as user ID and session role to Domain. Keep
  `Admin` unable to approve.
- **Rationale**: This follows the user's clarification and project role policy; no new
  authentication mechanism is needed.
- **Alternatives considered**: Keeping `Approver` as a Manager alias was rejected by the
  user's explicit selection.

### Use existing test tools and one UI rejection test

- **Decision**: Use current xUnit, Stryker.NET 4.13.0, and Playwright Test dependencies.
  Add exactly one browser test for a Manager rejecting an invoice of 50,001, asserting
  the visible reason and unchanged status.
- **Rationale**: These are already established by repository guidance and the feature
  requirements.
- **Alternatives considered**: Introducing a new test framework or adding multiple
  overlapping UI tests was rejected as unnecessary.

## Resolved Unknowns

No unresolved technology or integration choices remain. The selected stack is fixed by
the user and confirmed in project manifests. The concurrency behavior, role retirement,
identity normalization, and test scope are specified.
