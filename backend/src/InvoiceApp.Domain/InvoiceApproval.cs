namespace InvoiceApp.Domain;

/// <summary>One accepted approval in an invoice's ordered approval history.</summary>
public sealed record InvoiceApproval
{
    public InvoiceApproval(string approverId, string role, DateTime approvedAt)
    {
        ApproverId = approverId.Trim();
        Role = role;
        ApprovedAt = approvedAt.Kind == DateTimeKind.Utc
            ? approvedAt
            : approvedAt.ToUniversalTime();
    }

    public string ApproverId { get; }
    public string Role { get; }
    public DateTime ApprovedAt { get; }
}
