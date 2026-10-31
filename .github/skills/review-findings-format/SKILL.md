---
name: review-findings-format
description: Fixed output format for code review findings (Finding, Severity, Verdict, Reason). Use whenever reviewing a diff, reviewing tests, or writing the findings table for a pull request.
---

# Review findings format

Always output findings as a single Markdown table with exactly these columns, in this order:

```markdown
| Finding | Severity | Verdict | Reason |
| --- | --- | --- | --- |
| Approval is saved before the second-approver check runs | High | Pending | src/InvoiceApp.Domain/ApprovalService.cs line 58 |
```

## Rules

- **Finding**: one short sentence naming the problem and where (file and line).
- **Severity**: `High`, `Medium`, `Low` or `Info`.
- **Verdict**:
  - An agent reviewer always writes `Pending`. Only the human decides.
  - The human changes it to `Fixed` (give the commit hash in Reason), `Dismissed` (give why it is not a problem) or `Deferred` (give why it is out of scope and where it is tracked).
- **Reason**: never empty. Every verdict needs a reason.
- One row per finding. No findings grouped together, none left out.
- If there are no findings, output one row: `No findings | Info | Pending | Checked rules 1-6 against the diff`.

## Validate before pasting into the PR

```powershell
.\scripts\validate-review.ps1 -Path review-findings.md -AllowPending   # agent output
.\scripts\validate-review.ps1 -Path pr-description.md                  # final PR text
```

## Review focus for this repository

1. State changed before all approval rules pass.
2. The same-user and second-approver-must-be-Director rules.
3. Boundary values 50,000 / 50,001 and 500,000 / 500,001.
4. Weak tests (no unchanged-state assertion, asserting only non-null, no failure possible).
5. Layering: business logic outside `InvoiceApp.Domain`.
