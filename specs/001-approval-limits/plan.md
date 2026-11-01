# Implementation Plan: Approval Limits by Role

**Branch**: `001-approval-limits` | **Date**: 2026-10-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-approval-limits/spec.md`

## Summary

Add Manager and Director approval limits and a two-approver flow for invoices above
500,000. Keep role, amount, sequence, and identity validation in the Domain
`ApprovalService`; validate against an unchanged invoice and persist a complete updated
copy only after all applicable rules pass. Serialize approval attempts so later attempts
see earlier accepted approvals. Verify all rules with xUnit and Stryker, and add one
Playwright test for a Manager's rejected over-limit approval.

## Technical Context

**Language/Version**: C# on .NET 8

**Primary Dependencies**: ASP.NET Core 8; existing in-memory `IInvoiceRepository`;
`Microsoft.NET.Test.Sdk` 17.11.1; xUnit 2.9.2; Playwright Test 1.49.x

**Storage**: Existing in-memory repository; no database or migration

**Testing**: xUnit domain unit tests; Stryker.NET 4.13.0 mutation testing; one Playwright
end-to-end test

**Target Platform**: Existing ASP.NET Core web API and static browser UI

**Project Type**: Web application (API, domain, static UI, and end-to-end tests)

**Performance Goals**: No new performance target; preserve the existing local demo
behavior and keep approval operations synchronous

**Constraints**: Business rules and stable rejection reasons stay in Domain. Validate
before mutation or repository update. A single-process in-memory deployment serializes
approval operations through a shared service lock; approval attempts for different
invoices are also serialized to keep the coordination mechanism minimal.

**Scale/Scope**: Existing small demo application and in-memory invoice set; no
multi-instance or persistent-storage behavior is in scope.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle or gate | Plan alignment |
|---|---|
| Domain-owned business rules | Role eligibility, inclusive limits, distinct-user checks, second-approver rules, state validation, and rejection reasons remain in `InvoiceApp.Domain`. |
| Approval rule test coverage | xUnit covers every rule, both inclusive boundaries, and unchanged invoice state for every rejected approval. |
| Rejection atomicity | The service validates before mutation, builds an updated invoice copy, then replaces repository state only on success. |
| Small, single-purpose commits | Implement in two reviewable commits: single-approver limits and roles; then high-value second approval and related tests/UI coverage. |
| Layering and project conventions | API passes the authenticated ID and role to the service, maps domain errors through `Handle(...)`, and the UI displays returned errors. |
| End-to-end test conventions | One Playwright test uses page objects and accessible role/name locators for the Manager rejection flow. |

**Pre-design gate**: PASS. The `Approver` compatibility statement in the initial spec
conflicted with the project role policy. The user selected retiring that role; the spec
and checklist were aligned before planning.

**Post-design gate**: PASS. The proposed entity and contracts keep rules in Domain and
provide coverage and unchanged-state checks for rejection paths. No constitution
deviations are required.

## Project Structure

### Documentation (this feature)

```text
specs/001-approval-limits/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── approvals.md
├── checklists/
│   └── requirements.md
└── tasks.md              # Phase 2 output (/speckit-tasks); not created here
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── InvoiceApp.Domain/
│   │   ├── ApprovalService.cs
│   │   ├── Invoice.cs
│   │   └── approval domain types (new)
│   └── InvoiceApp.Api/
│       ├── Controllers/
│       │   └── ApprovalsController.cs
│       ├── Auth/
│       ├── InMemoryInvoiceRepository.cs
│       ├── Program.cs
│       ├── appsettings.json
│       └── wwwroot/
│           └── app.js
└── tests/
    └── InvoiceApp.Tests/
        ├── ApprovalServiceTests.cs
        └── FakeInvoiceRepository.cs
e2e/
├── pages/
└── tests/
    └── rejected-over-limit-approval.spec.ts (new)
```

**Structure Decision**: Extend the existing ASP.NET Core solution in place. Domain
types and validation belong in `InvoiceApp.Domain`; only authenticated identity
forwarding, role-based endpoint access, configuration, and HTTP/UI presentation change
in `InvoiceApp.Api`. Keep unit tests in the existing xUnit project and the browser test
in the existing Playwright project.

## Complexity Tracking

No constitution violations or additional projects are proposed.
