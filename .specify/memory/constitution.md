<!--
Sync Impact Report
Version change: unversioned scaffold -> 1.0.0 (initial substantive constitution)
Modified principles: none (the prior file contained only template placeholders)
Added principles: Domain-Owned Business Rules; Approval Rule Test Coverage;
  Rejection Atomicity; Small, Single-Purpose Commits
Added sections: Architecture and Quality Constraints; Development Workflow
Removed sections: none
Follow-up TODO: confirm the original ratification date
-->
# Invoice Approval Demo App Constitution

## Core Principles

### I. Domain-Owned Business Rules
Business rules MUST live in the Domain/service layer. API controllers MUST authenticate,
pass identity and role data to domain services, and map service results to HTTP responses;
they MUST NOT implement business rules. The UI MUST display decisions returned by the API
and MUST NOT decide business outcomes. This separation keeps approval behavior consistent
and independently testable.

### II. Approval Rule Test Coverage
Every approval rule MUST have a meaningful unit test in `backend/tests/InvoiceApp.Tests`.
Tests MUST assert exact boundary behavior and stable rejection reasons, and MUST prove that
rejected attempts leave the invoice unchanged. Tests MUST be strong enough to fail when the
rule they cover is implemented incorrectly.

### III. Rejection Atomicity
A rejected approval MUST return a clear reason and MUST NOT mutate any invoice data,
including status, approvers, amount, or timestamps. The service MUST validate every
applicable rule before changing state or persisting an update. This prevents partial
approval state and makes failures safe to retry or investigate.

### IV. Small, Single-Purpose Commits
Each commit MUST contain a cohesive, reviewable change with one primary purpose. Approval
rule changes MUST keep unrelated refactoring out of the same commit, and the relevant test
suite MUST pass before a commit is created. Small commits make behavior changes easier to
review and failures easier to isolate.

## Architecture and Quality Constraints

- The application is an ASP.NET Core 8 Web API with a static web UI.
- `InvoiceApp.Domain` owns invoice lifecycle and approval rules and MUST NOT depend on web,
  storage, or HTTP identity types.
- `InvoiceApp.Api` owns controllers, authentication, and repository implementations.
  Controllers MUST use `ApiControllerBase` and `Handle(...)` for service results.
- Tests MUST use the existing xUnit project and repository fakes where applicable.
- Approval role limits and rejection reasons belong in Domain code, not in controllers or
  UI code.
- End-to-end tests MUST use page objects and role-based locators rather than CSS or XPath
  selectors.

## Development Workflow

- Approval behavior changes MUST follow the Spec Kit workflow: specify, clarify when
  needed, plan, generate tasks, and analyze the design artifacts before implementation.
- Each new or changed approval rule MUST include tests for its allowed and rejected cases.
- Developers MUST read and review generated tests to confirm they verify behavior rather
  than merely asserting success-shaped outcomes.
- Run `dotnet test backend/InvoiceApp.sln` before each commit; do not commit when it fails.
- Approval rule tests MUST be checked with Stryker mutation testing; surviving rule
  mutants MUST be killed or explained.
- Review changes for layering, rejection atomicity, and the specific approval rules before
  committing.

## Governance

This constitution governs project design and delivery practices. When other project
guidance conflicts with it, the conflict MUST be resolved before implementation rather
than silently choosing one source. Amendments MUST be made in this file, reviewed for
consistency with the project, and recorded with an updated version and amendment date.
Versioning follows semantic versioning: MAJOR for incompatible governance changes, MINOR
for new principles or materially expanded guidance, and PATCH for clarifications and
non-semantic refinements.

Every implementation review MUST verify compliance with the applicable principles,
including domain ownership, test coverage, and unchanged invoice state on rejection.
Deviations require an explicit, reviewed constitution amendment; implementation convenience
alone is not sufficient justification.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE): confirm original adoption date | **Last Amended**: 2026-10-31
