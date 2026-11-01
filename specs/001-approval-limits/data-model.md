# Data Model: Approval Limits by Role

## Invoice

The existing invoice remains the aggregate whose lifecycle is managed by
`ApprovalService`.

| Field | Type | Constraints and meaning |
|---|---|---|
| `Id` | String | Stable invoice identifier. |
| `CustomerName` | String | Existing invoice customer display value. |
| `Amount` | Decimal | Approval thresholds are inclusive: Manager up to 50,000; Director up to 500,000. |
| `Status` | Invoice status | An approval can be attempted only in `PendingApproval`. An invoice above 500,000 remains pending after its first accepted approval and becomes `Approved` after its second accepted approval. |
| `CreatedAt` | UTC timestamp | Existing creation timestamp; approval attempts do not change it. |
| `Approvals` | Ordered collection of Approval | Accepted approvals for this invoice in decision order. A rejected attempt never adds an entry. |
| `DecidedBy` | Nullable string | Compatibility field containing the user ID of the latest accepted approval. |
| `DecidedAt` | Nullable UTC timestamp | Compatibility field containing the time of the latest accepted approval. |
| `DecisionNote` | Nullable string | Existing decision note; approval rejection does not change it. |

Existing invoices begin with an empty approval history unless their persisted data
already includes approval records. An existing `DecidedBy` value alone does not count
as a first approval for a pending high-value invoice; an identity and role record is
required to safely establish distinct approvers.

## Approval

An immutable record of one accepted approval.

| Field | Type | Constraints and meaning |
|---|---|---|
| `ApproverId` | String | User ID trimmed of surrounding whitespace. Compared case-insensitively to all prior approval IDs for the invoice. |
| `Role` | String or role enum | Exactly `Manager` or `Director`; the retired `Approver` and `Admin` roles are not eligible. |
| `ApprovedAt` | UTC timestamp | Time this approval was accepted. |

Approval entries are ordered by acceptance. The first approval on an invoice above
500,000 may be by a Manager or Director. The second must be by a distinct Director.
An invoice at or below 500,000 completes with one allowed approval; a Manager's
single approval above 50,000 is rejected.

## State and Validation

| Current state / condition | Attempt | Result |
|---|---|---|
| Not `PendingApproval` | Any approval | Reject with a clear state reason; no invoice field changes. |
| Pending, amount ≤ 50,000 | Manager | Add one approval; set status to `Approved`. |
| Pending, amount ≤ 500,000 | Director | Add one approval; set status to `Approved`. |
| Pending, 50,000 < amount ≤ 500,000 | Manager | Reject with Manager limit reason; no changes. |
| Pending, amount > 500,000, no approvals | Manager or Director | Record first approval; keep status `PendingApproval`. |
| Pending, amount > 500,000, one approval | Same user ID | Reject duplicate; no changes. |
| Pending, amount > 500,000, one approval | Distinct Manager | Reject second-role rule; no changes. |
| Pending, amount > 500,000, one approval | Distinct Director | Add second approval; set status to `Approved`. |
| Any state | Admin or unknown role | Reject as ineligible; no changes. |

All applicable validation happens before repository update. Concurrent approval attempts
are serialized; each later attempt validates against the invoice produced by the
preceding accepted attempt.

## Relationships

- One Invoice has zero or more ordered Approval records.
- Each Approval belongs to exactly one Invoice and identifies one authenticated user.
- No user ID, after trim and case-insensitive comparison, may appear twice in one
  invoice's approval history.
