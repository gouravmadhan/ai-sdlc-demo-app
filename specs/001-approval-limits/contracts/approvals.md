# Approval API Contract

## Authentication and authorization

- Requests require the existing authenticated session, supplied through the API's
  bearer-token mechanism.
- The approval endpoint permits `Manager` and `Director` roles.
- `Admin`, the retired `Approver` role, and unknown roles cannot approve.
- The API passes the authenticated username as the user ID and the session role to
  Domain. The API does not enforce amount or approval-sequence rules.

## Approve an invoice

`POST /api/approvals/{invoiceId}/approve`

No request body is required. The caller identity and role come from the authenticated
session.

### Accepted response

`200 OK` with the invoice JSON, including its approval history.

Example first-approval response for a high-value invoice (timestamps abbreviated):

```json
{
  "id": "INV-2001",
  "amount": 600000,
  "status": "PendingApproval",
  "approvals": [
    {
      "approverId": "demo.manager",
      "role": "Manager",
      "approvedAt": "2026-10-31T12:00:00Z"
    }
  ],
  "decidedBy": "demo.manager",
  "decidedAt": "2026-10-31T12:00:00Z"
}
```

- A Manager approval at or below 50,000 completes the invoice.
- A Director approval at or below 500,000 completes the invoice.
- For an invoice above 500,000, a first valid approval returns `200 OK` while the
  invoice remains `PendingApproval` with one recorded approval.
- A distinct Director's valid second approval returns `200 OK` with status `Approved`
  and two approval records.

### Rejected response

Domain rule rejections are mapped by the existing API error handler to `409 Conflict`:

```json
{
  "error": "Manager limit is 50,000"
}
```

The `error` value is a stable, clear reason identifying the failed rule. Examples:

- `Manager limit is 50,000`
- `Second approver must be a Director`
- `Same user cannot approve twice`
- A clear reason that the invoice must be pending or that the caller's role cannot
  approve

For every domain rule rejection, the invoice JSON and stored invoice remain unchanged,
including approval history and timestamps.

### Other outcomes

- `401 Unauthorized`: no valid authenticated session.
- `403 Forbidden`: authenticated session has a role not allowed to call the approval
  endpoint.
- `404 Not Found`: invoice ID does not exist.

## UI behavior

The static invoice UI calls the same endpoint. On rejection it displays the API's
`error` message and refreshes or retains the invoice so its status remains unchanged.
The over-limit acceptance scenario signs in as a Manager and attempts to approve a
pending invoice of 50,001.
