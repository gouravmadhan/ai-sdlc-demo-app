---
name: approval-rules
description: Domain rules for approving invoices by role (Manager and Director limits, two-approver rule above 500,000, same-user rule, no state change on rejection). Use when implementing, testing or reviewing anything in ApprovalService or the approval endpoints.
---

# Approval rules skill

`AGENTS.md` is the authority. This skill is the quick checklist for applying it.

## Rules

1. Manager approves up to 50,000 (inclusive).
2. Director approves up to 500,000 (inclusive).
3. Above 500,000: two distinct approvers; the second must be a Director.
4. The same user (by user ID, case-insensitive, trimmed) never counts as both approvers.
5. A rejected attempt returns a clear reason and changes nothing on the invoice.
6. Only `Manager` and `Director` can approve. `Admin` cannot.

## Implementation checklist

- Rules live in `InvoiceApp.Domain` (`ApprovalService`). Controllers only map the result.
- Limits are named constants, not magic numbers.
- Order inside the service: check role, check status is `PendingApproval`, check limit, check approver rules, and only then change state and save.
- Never record the first approver or change status before every check has passed.
- Rejection reasons are specific, for example `Manager limit is 50,000`, `Second approver must be a Director`, `Same user cannot approve twice`.

## Test matrix (every row needs a test)

| Case | Expected |
| --- | --- |
| Manager, amount 50,000 | Approved |
| Manager, amount 50,001 | Rejected, invoice unchanged |
| Director, amount 500,000 | Approved with one approver |
| Director, amount 500,001, first approval | Stays pending, needs second approver |
| Director + different Director, amount above 500,000 | Approved |
| Director + Manager as second, amount above 500,000 | Rejected, invoice unchanged |
| Same user ID twice (also different casing or spaces) | Rejected, invoice unchanged |
| Admin tries to approve | Rejected, invoice unchanged |
| Invoice not in PendingApproval | Rejected, invoice unchanged |

Every rejection test must compare the stored invoice before and after the attempt, not only check the reason.

## Common agent mistakes to look for

- State changed before validation finishes (partial approval left behind).
- Comparing users by object or display name instead of ID.
- `<` used where `<=` is required at 50,000 and 500,000.
- Checking that the second approver is a Director but not checking the first approver at all.
- Business logic added in a controller or `Program.cs`.
