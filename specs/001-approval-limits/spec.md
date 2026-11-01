# Feature Specification: Approval Limits by Role

**Feature Branch**: `001-approval-limits`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "Approval limits by role for the invoice approval service. A Manager may approve up to 50,000 inclusive. A Director may approve up to 500,000 inclusive. Above 500,000 needs two distinct approvers and the second must be a Director. The same user can never count as both approvers on one invoice. A rejected attempt returns a clear reason and leaves the invoice completely unchanged."

## Clarifications

### Session 2026-10-31

- Q: If two users try to approve the same high-value invoice at the same time, how should the system handle the competing attempts? → A: Process one attempt first, then evaluate the other using the updated invoice and applicable first/second-approver rules.
- Q: Which policy should the plan follow for the legacy `Approver` role? → A: Retire `Approver`; use Manager and Director only.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Approve within role limits (Priority: P1)

An approver reviews a pending invoice and approves it when the invoice amount is within
their role's single-approver limit. Only users with the Manager or Director role may
approve; users with the Admin role alone are not approvers.

**Why this priority**: Role-specific limits establish the core authorization behavior for
ordinary approvals.

**Independent Test**: Submit pending invoices at and just above each single-approver
boundary and verify the allowed approval and the rejected attempt.

**Acceptance Scenarios**:

1. **Given** a pending invoice for 50,000 and a Manager, **When** the Manager approves it,
   **Then** the invoice becomes Approved and records that Manager as the approver.
2. **Given** a pending invoice for 50,001 and a Manager, **When** the Manager attempts to
   approve it, **Then** the attempt is rejected with a reason identifying the 50,000 limit
   and the invoice remains exactly as it was.
3. **Given** a pending invoice for 500,000 and a Director, **When** the Director approves
   it, **Then** the invoice becomes Approved with that single approval.
4. **Given** a pending invoice for 500,001 and a Director, **When** the Director records
   the first approval, **Then** the invoice remains PendingApproval awaiting a second
   approver.

---

### User Story 2 - Complete a high-value approval (Priority: P2)

Approvers complete an invoice approval above 500,000 using two different users, with a
Director providing the second approval.

**Why this priority**: Requiring independent approval for high-value invoices protects
against a single user authorizing an unusually large payment.

**Independent Test**: Record a first approval on a pending invoice above 500,000, then
submit a qualifying second approval and verify the invoice completes only after the
second approval.

**Acceptance Scenarios**:

1. **Given** a pending invoice above 500,000, **When** a Manager or Director records the
   first approval, **Then** the invoice remains PendingApproval and records that approval.
2. **Given** an invoice above 500,000 with a first approval, **When** a different Director
   approves it, **Then** the invoice becomes Approved and records both distinct approvers.
3. **Given** an invoice above 500,000 with a first approval, **When** a Manager attempts
   the second approval, **Then** the attempt is rejected with a reason that the second
   approver must be a Director, and the invoice is unchanged.
4. **Given** an invoice above 500,000 with a first approval, **When** the same user
   attempts the second approval using an ID that differs only in casing or surrounding
   whitespace, **Then** the attempt is rejected as a duplicate and the invoice is
   unchanged.
5. **Given** two approval attempts arrive at the same time for one invoice above 500,000,
   **When** one attempt is processed before the other, **Then** the later attempt is
   evaluated against the invoice after the first attempt, as a second approval.

---

### User Story 3 - Understand rejected approval attempts (Priority: P3)

An approver receives a clear explanation when an approval cannot be completed and can
confirm that the rejected attempt did not alter the invoice.

**Why this priority**: Clear, safe rejection behavior prevents users from mistaking a
failed attempt for an approval or losing existing invoice data.

**Independent Test**: Attempt approval with an unauthorized role, an invoice outside the
allowed state, or an invalid approver sequence; verify the reason and compare the invoice
before and after.

**Acceptance Scenarios**:

1. **Given** a pending invoice and a user with no approval role, **When** the user attempts
   to approve it, **Then** the attempt is rejected with a clear reason and the invoice is
   unchanged.
2. **Given** an invoice that is not PendingApproval, **When** a user attempts to approve
   it, **Then** the attempt is rejected with a clear reason and the invoice is unchanged.
3. **Given** any rejected approval attempt, **When** the user sees the result, **Then**
   the reason identifies the failed rule and all invoice data—including status, approvers,
   amount, and timestamps—matches its value before the attempt.
4. **Given** a Manager signed in to the app and a pending invoice above 50,000 but no more
   than 500,000, **When** the Manager tries to approve it through the UI, **Then** the
   visible result explains the Manager limit and the invoice status remains unchanged.

## Edge Cases

- Exactly 50,000 is within the Manager limit; 50,001 is above it.
- Exactly 500,000 is within the Director single-approval limit; any amount above it
  requires two approvals.
- For an invoice above 500,000, the first approver may be a Manager or a Director; the
  second approver must be a Director.
- Approver identity is determined by user ID after trimming surrounding whitespace and
  comparing without regard to casing; display names do not establish distinctness.
- An Admin without a Manager or Director approval role cannot approve.
- Approval is permitted only while the invoice is PendingApproval.
- A rejected second approval preserves the accepted first approval and every other
  invoice field exactly as they were before the rejected attempt.
- Overlapping attempts for one invoice are processed in a definite order; each later
  attempt is evaluated against the updated invoice and current approval sequence.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a Manager to approve a pending invoice with an amount up
  to and including 50,000, except that a Manager may provide the first approval on an
  invoice above 500,000 as part of the required two-approver sequence.
- **FR-002**: System MUST reject a Manager's single approval for an invoice above 50,000
  and up to and including 500,000, and MUST provide a clear reason identifying the
  Manager limit.
- **FR-003**: System MUST allow a Director to approve a pending invoice up to and
  including 500,000 with one approval.
- **FR-004**: System MUST require two distinct approvers for an invoice above 500,000.
  The first approver MUST be a Manager or Director, and the second MUST be a Director.
- **FR-005**: System MUST reject a second approval when it is from a Manager, and MUST
  return a clear reason identifying the second-approver requirement.
- **FR-006**: System MUST determine whether two approvals are distinct by comparing
  normalized user IDs: trim surrounding whitespace and compare case-insensitively.
- **FR-007**: System MUST reject an attempt by the same user to count as both approvers,
  and MUST return a clear reason identifying the duplicate-approver rule.
- **FR-008**: Only users with the `Manager` or `Director` role MUST be allowed to approve.
  The legacy `Approver` role MUST NOT grant approval privileges; Manager users use the
  `demo.manager` identity.
- **FR-009**: System MUST reject an approval attempt when the invoice is not
  PendingApproval and MUST return a clear reason.
- **FR-010**: Every rejected attempt MUST leave the invoice completely unchanged,
  including status, approvers, amount, and timestamps. All applicable rules MUST be
  checked before invoice data is changed.
- **FR-011**: System MUST expose the rejection reason to the user initiating the
  approval, including through the invoice approval UI.
- **FR-012**: The app MUST provide demo identities for a Manager, a Director, and a
  second Director so the two-approver flow can be exercised.
- **FR-013**: When approval attempts overlap for the same invoice, the system MUST
  process them in a definite order and evaluate each later attempt against the updated
  invoice and applicable first- or second-approver rules.

### Key Entities *(include if feature involves data)*

- **Invoice**: A submitted item requiring approval; includes an amount, lifecycle status,
  approval history, and timestamps.
- **Approver**: An authenticated user with a Manager or Director role, identified by a
  user ID.
- **Approval**: A user's decision on a pending invoice, associated with the user's ID and
  the invoice's approval sequence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All four boundary outcomes are correct: a Manager succeeds at 50,000 and is
  rejected at 50,001; a Director succeeds at 500,000 and an invoice above 500,000 remains
  pending after only its first approval.
- **SC-002**: Every invoice above 500,000 is approved only after two distinct approvers
  have acted and the second is a Director.
- **SC-003**: Every rejected approval scenario returns a rule-specific reason and leaves
  every invoice field identical to its pre-attempt value.
- **SC-004**: A Manager's rejected over-limit attempt through the UI displays the
  rejection reason and leaves the invoice status unchanged.
- **SC-005**: Case-only or whitespace-only differences in a user ID never allow one user
  to satisfy the two-approver requirement twice.

## Assumptions

- Monetary amounts are expressed in the same currency and precision already used by the
  invoice records; the specified limits are inclusive.
- The legacy `Approver` role is retired rather than kept as a Manager alias.
- For amounts above 500,000, a Manager's first approval is permitted as part of the
  two-approver process; a Director must provide the second approval.
- Rejections use stable, specific reasons that name the failed rule. Examples include
  `Manager limit is 50,000`, `Second approver must be a Director`, and
  `Same user cannot approve twice`.
- Demo identities are for local demonstration and testing only; this specification does
  not define production identity provisioning.
- An invoice may record its first qualifying approval while remaining PendingApproval;
  that accepted first approval is not a rejected attempt.
