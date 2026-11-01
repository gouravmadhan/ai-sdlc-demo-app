namespace InvoiceApp.Domain;

/// <summary>
/// Moves an invoice through its lifecycle and applies role-aware approval rules.
/// </summary>
public class ApprovalService
{
    public const decimal ManagerApprovalLimit = 50_000m;
    public const decimal DirectorApprovalLimit = 500_000m;

    private readonly IInvoiceRepository _repository;
    private readonly object _approvalLock = new();

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        var invoice = Find(invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
        }

        invoice.Status = InvoiceStatus.PendingApproval;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Approve(string invoiceId, string userId, string role)
    {
        lock (_approvalLock)
        {
            var invoice = Find(invoiceId);
            ValidateApproval(invoice, userId, role);

            var normalizedUserId = userId.Trim();
            var approvedAt = DateTime.UtcNow;
            var updatedInvoice = Copy(invoice);
            updatedInvoice.Approvals.Add(new InvoiceApproval(normalizedUserId, role, approvedAt));
            updatedInvoice.DecidedBy = normalizedUserId;
            updatedInvoice.DecidedAt = approvedAt;

            if (invoice.Amount <= DirectorApprovalLimit || invoice.Approvals.Count > 0)
            {
                updatedInvoice.Status = InvoiceStatus.Approved;
            }

            _repository.Update(updatedInvoice);
            return updatedInvoice;
        }
    }

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        invoice.Status = InvoiceStatus.Rejected;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = reason;
        _repository.Update(invoice);
        return invoice;
    }

    private Invoice Find(string invoiceId) =>
        _repository.GetById(invoiceId)
        ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }

    private static void ValidateApproval(Invoice invoice, string userId, string role)
    {
        if (role is not ("Manager" or "Director"))
        {
            throw new InvalidOperationException($"Role {role} cannot approve invoices");
        }

        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be approved.");
        }

        if (invoice.Amount > DirectorApprovalLimit)
        {
            if (invoice.Approvals.Count > 0 &&
                invoice.Approvals.Any(approval =>
                    string.Equals(
                        approval.ApproverId,
                        userId.Trim(),
                        StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Same user cannot approve twice");
            }

            if (invoice.Approvals.Count > 0 && role != "Director")
            {
                throw new InvalidOperationException("Second approver must be a Director");
            }

            if (invoice.Approvals.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Invoice {invoice.Id} already has two approvals.");
            }

            return;
        }

        if (role == "Manager" && invoice.Amount > ManagerApprovalLimit)
        {
            throw new InvalidOperationException("Manager limit is 50,000");
        }
    }

    private static Invoice Copy(Invoice invoice) =>
        new()
        {
            Id = invoice.Id,
            CustomerName = invoice.CustomerName,
            Amount = invoice.Amount,
            Status = invoice.Status,
            CreatedAt = invoice.CreatedAt,
            Approvals = invoice.Approvals.ToList(),
            DecidedBy = invoice.DecidedBy,
            DecidedAt = invoice.DecidedAt,
            DecisionNote = invoice.DecisionNote
        };
}
