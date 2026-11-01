using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private static Invoice InvoiceWith(string id, InvoiceStatus status) =>
        new()
        {
            Id = id,
            CustomerName = "Test School",
            Amount = 100m,
            Status = status,
            CreatedAt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            DecidedBy = "prior.user",
            DecidedAt = new DateTime(2024, 1, 3, 4, 5, 6, DateTimeKind.Utc),
            DecisionNote = "Existing decision data"
        };

    private static Invoice PendingInvoiceWithoutDecision(string id)
    {
        var invoice = InvoiceWith(id, InvoiceStatus.PendingApproval);
        invoice.DecidedBy = null;
        invoice.DecidedAt = null;
        return invoice;
    }

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    private sealed class UpdateRecordingInvoiceRepository : IInvoiceRepository
    {
        private readonly FakeInvoiceRepository _inner;

        public UpdateRecordingInvoiceRepository(params Invoice[] invoices)
        {
            _inner = new FakeInvoiceRepository(invoices);
        }

        public List<Invoice> UpdatedInvoices { get; } = new();

        public IReadOnlyList<Invoice> GetAll() => _inner.GetAll();

        public Invoice? GetById(string id) => _inner.GetById(id);

        public IReadOnlyList<Invoice> GetByIds(IEnumerable<string> ids) => _inner.GetByIds(ids);

        public Invoice Add(Invoice invoice) => _inner.Add(invoice);

        public void Update(Invoice invoice)
        {
            UpdatedInvoices.Add(invoice);
            _inner.Update(invoice);
        }

        public bool Remove(string id) => _inner.Remove(id);
    }

    private sealed record InvoiceSnapshot(
        string Id,
        string CustomerName,
        decimal Amount,
        InvoiceStatus Status,
        DateTime CreatedAt,
        string? DecidedBy,
        DateTime? DecidedAt,
        string? DecisionNote);

    private static InvoiceSnapshot Capture(Invoice invoice) =>
        new(invoice.Id, invoice.CustomerName, invoice.Amount, invoice.Status, invoice.CreatedAt,
            invoice.DecidedBy, invoice.DecidedAt, invoice.DecisionNote);

    private static void AssertRejectedWithoutChanges(
        FakeInvoiceRepository repository,
        Invoice invoice,
        Action attempt,
        string expectedReason)
    {
        var storedBefore = repository.GetById(invoice.Id)!;
        var before = Capture(storedBefore);

        var exception = Assert.Throws<InvalidOperationException>(attempt);

        Assert.Equal(expectedReason, exception.Message);
        var storedAfter = repository.GetById(invoice.Id)!;
        Assert.Equal(before, Capture(storedAfter));
    }

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);
        var repository = new UpdateRecordingInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal(new[] { invoice }, repository.UpdatedInvoices);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Approved);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        var before = Capture(repository.GetById("INV-1")!);

        var exception = Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));

        Assert.Equal("Invoice INV-1 is Approved; only drafts can be submitted.", exception.Message);
        Assert.Equal(before, Capture(repository.GetById("INV-1")!));
    }

    [Fact]
    public void Invoice_DefaultsIdAndCustomerNameToEmptyStrings()
    {
        var invoice = new Invoice();

        Assert.Equal(string.Empty, invoice.Id);
        Assert.Equal(string.Empty, invoice.CustomerName);
    }

    [Fact]
    public void Manager_At50000_IsApproved()
    {
        var invoice = PendingInvoiceWithoutDecision("INV-1");
        invoice.Amount = 50_000m;
        var repository = new UpdateRecordingInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        var beforeApproval = DateTime.UtcNow;

        var result = service.Approve("INV-1", "demo.manager", "Manager");
        var afterApproval = DateTime.UtcNow;

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.InRange(result.DecidedAt.Value, beforeApproval, afterApproval);
        Assert.Equal(new[] { invoice }, repository.UpdatedInvoices);
    }

    [Fact]
    public void Manager_Above50000_IsRejectedWithoutChangingInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 50_001m;
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        AssertRejectedWithoutChanges(repository, invoice,
            () => service.Approve("INV-1", "demo.manager", "Manager"),
            "Manager limit is 50,000");
    }

    [Fact]
    public void Director_At500000_IsApproved()
    {
        var invoice = PendingInvoiceWithoutDecision("INV-1");
        invoice.Amount = 500_000m;
        var service = ServiceFor(invoice);
        var beforeApproval = DateTime.UtcNow;

        var result = service.Approve("INV-1", "demo.director", "Director");
        var afterApproval = DateTime.UtcNow;

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.InRange(result.DecidedAt.Value, beforeApproval, afterApproval);
    }

    [Fact]
    public void Director_Above500000_IsRejectedWithoutChangingInvoice()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 500_001m;
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        AssertRejectedWithoutChanges(repository, invoice,
            () => service.Approve("INV-1", "demo.director", "Director"),
            "Invoices above 500,000 require two approvers");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Unknown")]
    public void NonApproverRole_IsRejectedWithoutChangingInvoice(string role)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        AssertRejectedWithoutChanges(repository, invoice,
            () => service.Approve("INV-1", "demo.user", role),
            $"Role {role} cannot approve invoices");
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void InvoiceNotPending_IsRejectedWithoutChangingInvoice(InvoiceStatus status)
    {
        var invoice = InvoiceWith("INV-1", status);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        AssertRejectedWithoutChanges(repository, invoice,
            () => service.Approve("INV-1", "demo.manager", "Manager"),
            $"Invoice INV-1 is {status}; only pending invoices can be approved.");
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        var exception = Assert.Throws<KeyNotFoundException>(
            () => service.Approve("INV-404", "demo.manager", "Manager"));

        Assert.Equal("Invoice INV-404 was not found.", exception.Message);
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        var repository = new UpdateRecordingInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Reject("INV-1", "demo.manager", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
        Assert.Equal(new[] { invoice }, repository.UpdatedInvoices);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Reject_InvoiceNotPending_IsRejectedWithoutChangingInvoice(InvoiceStatus status)
    {
        var invoice = InvoiceWith("INV-1", status);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        AssertRejectedWithoutChanges(repository, invoice,
            () => service.Reject("INV-1", "demo.manager", "Duplicate invoice"),
            $"Invoice INV-1 is {status}; only pending invoices can be decided.");
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        var before = Capture(repository.GetById("INV-1")!);

        var exception = Assert.Throws<ArgumentException>(
            () => service.Reject("INV-1", "demo.manager", "  "));

        Assert.Equal("A reason is required to reject an invoice. (Parameter 'reason')", exception.Message);
        Assert.Equal(before, Capture(repository.GetById("INV-1")!));
    }
}
