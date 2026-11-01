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

    private static Invoice HighValuePendingInvoice(string id = "INV-1")
    {
        var invoice = PendingInvoiceWithoutDecision(id);
        invoice.Amount = 500_001m;
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
        public List<InvoiceSnapshot> UpdatedSnapshots { get; } = new();

        public IReadOnlyList<Invoice> GetAll() => _inner.GetAll();

        public Invoice? GetById(string id) => _inner.GetById(id);

        public IReadOnlyList<Invoice> GetByIds(IEnumerable<string> ids) => _inner.GetByIds(ids);

        public Invoice Add(Invoice invoice) => _inner.Add(invoice);

        public void Update(Invoice invoice)
        {
            UpdatedInvoices.Add(invoice);
            _inner.Update(invoice);
            UpdatedSnapshots.Add(Capture(invoice));
        }

        public bool Remove(string id) => _inner.Remove(id);
    }

    private sealed record ApprovalSnapshot(string ApproverId, string Role, DateTime ApprovedAt);

    private sealed record InvoiceSnapshot(
        string Id,
        string CustomerName,
        decimal Amount,
        InvoiceStatus Status,
        DateTime CreatedAt,
        string? DecidedBy,
        DateTime? DecidedAt,
        string? DecisionNote,
        IReadOnlyList<ApprovalSnapshot> Approvals);

    private static InvoiceSnapshot Capture(Invoice invoice) =>
        new(invoice.Id, invoice.CustomerName, invoice.Amount, invoice.Status, invoice.CreatedAt,
            invoice.DecidedBy, invoice.DecidedAt, invoice.DecisionNote,
            Array.AsReadOnly(invoice.Approvals
                .Select(approval => new ApprovalSnapshot(
                    approval.ApproverId, approval.Role, approval.ApprovedAt))
                .ToArray()));

    private static void AssertInvoiceMatches(InvoiceSnapshot expected, Invoice actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.CustomerName, actual.CustomerName);
        Assert.Equal(expected.Amount, actual.Amount);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        Assert.Equal(expected.DecidedBy, actual.DecidedBy);
        Assert.Equal(expected.DecidedAt, actual.DecidedAt);
        Assert.Equal(expected.DecisionNote, actual.DecisionNote);
        Assert.Equal(expected.Approvals, actual.Approvals
            .Select(approval => new ApprovalSnapshot(
                approval.ApproverId, approval.Role, approval.ApprovedAt))
            .ToArray());
    }

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
        AssertInvoiceMatches(before, storedAfter);
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
        AssertInvoiceMatches(before, repository.GetById("INV-1")!);
    }

    [Fact]
    public void Invoice_DefaultsIdAndCustomerNameToEmptyStrings()
    {
        var invoice = new Invoice();

        Assert.Equal(string.Empty, invoice.Id);
        Assert.Equal(string.Empty, invoice.CustomerName);
    }

    [Fact]
    public void InvoiceApproval_StoresTrimmedApproverIdAndUtcApprovalTime()
    {
        var approvedAt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        var approval = new InvoiceApproval("  Demo.Manager  ", "Manager", approvedAt);

        Assert.Equal("Demo.Manager", approval.ApproverId);
        Assert.Equal("Manager", approval.Role);
        Assert.Equal(approvedAt, approval.ApprovedAt);
        Assert.Equal(DateTimeKind.Utc, approval.ApprovedAt.Kind);
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
        Assert.Same(result, Assert.Single(repository.UpdatedInvoices));
        Assert.Single(result.Approvals);
        Assert.Equal("demo.manager", result.Approvals[0].ApproverId);
        Assert.Equal("Manager", result.Approvals[0].Role);
        Assert.Equal(result.DecidedAt, result.Approvals[0].ApprovedAt);
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
        Assert.Single(result.Approvals);
        Assert.Equal("demo.director", result.Approvals[0].ApproverId);
        Assert.Equal("Director", result.Approvals[0].Role);
        Assert.Equal(result.DecidedAt, result.Approvals[0].ApprovedAt);
    }

    [Fact]
    public void Director_FirstApproval_Above500k_LeavesInvoicePendingWithOneApproval()
    {
        var invoice = HighValuePendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        var beforeApproval = DateTime.UtcNow;

        var result = service.Approve("INV-1", "demo.director", "Director");
        var afterApproval = DateTime.UtcNow;

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        var approval = Assert.Single(result.Approvals);
        Assert.Equal("demo.director", approval.ApproverId);
        Assert.Equal("Director", approval.Role);
        Assert.InRange(approval.ApprovedAt, beforeApproval, afterApproval);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.Equal(approval.ApprovedAt, result.DecidedAt);
        Assert.Same(result, repository.GetById("INV-1"));
    }

    [Fact]
    public void Manager_FirstApproval_Above500k_LeavesInvoicePendingWithOneApproval()
    {
        var invoice = HighValuePendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        var result = service.Approve("INV-1", " demo.manager ", "Manager");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        var approval = Assert.Single(result.Approvals);
        Assert.Equal("demo.manager", approval.ApproverId);
        Assert.Equal("Manager", approval.Role);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.Equal(approval.ApprovedAt, result.DecidedAt);
        Assert.Same(result, repository.GetById("INV-1"));
    }

    [Fact]
    public void TwoDistinctDirectors_Above500k_CompleteApproval()
    {
        var invoice = HighValuePendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        service.Approve("INV-1", "demo.director", "Director");
        var beforeSecondApproval = DateTime.UtcNow;
        var result = service.Approve("INV-1", "demo.director2", "Director");
        var afterSecondApproval = DateTime.UtcNow;

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal(new[] { "demo.director", "demo.director2" },
            result.Approvals.Select(approval => approval.ApproverId));
        Assert.All(result.Approvals, approval => Assert.Equal("Director", approval.Role));
        Assert.Equal("demo.director2", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.InRange(result.DecidedAt.Value, beforeSecondApproval, afterSecondApproval);
        Assert.Equal(result.Approvals[1].ApprovedAt, result.DecidedAt);
        Assert.Same(result, repository.GetById("INV-1"));
    }

    [Fact]
    public void Manager_FirstThenDirectorSecond_Above500k_CompletesApproval()
    {
        var invoice = HighValuePendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);

        service.Approve("INV-1", "demo.manager", "Manager");
        var result = service.Approve("INV-1", "demo.director", "Director");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal(new[] { "demo.manager", "demo.director" },
            result.Approvals.Select(approval => approval.ApproverId));
        Assert.Equal(new[] { "Manager", "Director" },
            result.Approvals.Select(approval => approval.Role));
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.Equal(result.Approvals[1].ApprovedAt, result.DecidedAt);
    }

    [Fact]
    public void DirectorThenManagerAsSecond_Above500k_IsRejected()
    {
        var invoice = HighValuePendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "demo.director", "Director");

        AssertRejectedWithoutChanges(repository, invoice,
            () => service.Approve("INV-1", "demo.manager", "Manager"),
            "Second approver must be a Director");

        var stored = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.PendingApproval, stored.Status);
        Assert.Single(stored.Approvals);
    }

    [Fact]
    public void TwoDifferentManagers_Above500k_SecondIsRejected()
    {
        var invoice = HighValuePendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "manager.one", "Manager");

        AssertRejectedWithoutChanges(repository, invoice,
            () => service.Approve("INV-1", "manager.two", "Manager"),
            "Second approver must be a Director");

        var stored = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.PendingApproval, stored.Status);
        Assert.Single(stored.Approvals);
    }

    [Fact]
    public void SameDirectorTwice_Above500k_IsRejected()
    {
        var invoice = HighValuePendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "demo.director", "Director");

        AssertRejectedWithoutChanges(repository, invoice,
            () => service.Approve("INV-1", "demo.director", "Director"),
            "Same user cannot approve twice");

        var stored = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.PendingApproval, stored.Status);
        Assert.Single(stored.Approvals);
    }

    [Fact]
    public void SameManagerTwice_Above500k_IsRejected()
    {
        var invoice = HighValuePendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "demo.manager", "Manager");

        AssertRejectedWithoutChanges(repository, invoice,
            () => service.Approve("INV-1", "demo.manager", "Manager"),
            "Same user cannot approve twice");

        var stored = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.PendingApproval, stored.Status);
        Assert.Single(stored.Approvals);
    }

    [Fact]
    public void SameUserIdWithDifferentCasingOrWhitespace_IsRejected()
    {
        var invoice = HighValuePendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "  Demo.Director  ", "Director");

        AssertRejectedWithoutChanges(repository, invoice,
            () => service.Approve("INV-1", " demo.director ", "Director"),
            "Same user cannot approve twice");

        var stored = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.PendingApproval, stored.Status);
        Assert.Single(stored.Approvals);
        Assert.Equal("Demo.Director", stored.Approvals[0].ApproverId);
    }

    [Fact]
    public void ThirdApproval_AfterCompletion_IsRejectedAsWrongStatus()
    {
        var invoice = HighValuePendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "demo.director", "Director");
        service.Approve("INV-1", "demo.director2", "Director");

        AssertRejectedWithoutChanges(repository, invoice,
            () => service.Approve("INV-1", "demo.manager", "Manager"),
            "Invoice INV-1 is Approved; only pending invoices can be approved.");
    }

    [Fact]
    public async Task ConcurrentApprovals_BySameUser_RecordOnlyOneApproval()
    {
        var invoice = HighValuePendingInvoice();
        var repository = new UpdateRecordingInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        using var start = new ManualResetEventSlim(false);

        var attempts = Enumerable.Range(0, 2)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                try
                {
                    service.Approve("INV-1", "demo.director", "Director");
                    return (Succeeded: true, Error: (InvalidOperationException?)null);
                }
                catch (InvalidOperationException error)
                {
                    return (Succeeded: false, Error: error);
                }
            }))
            .ToArray();

        start.Set();
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result.Succeeded);
        var rejected = Assert.Single(results, result => !result.Succeeded);
        Assert.Equal("Same user cannot approve twice", rejected.Error!.Message);
        var stored = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.PendingApproval, stored.Status);
        Assert.Single(stored.Approvals);
        // Captured during the successful update while the service lock is held,
        // this is the complete state observed by the rejected serialized attempt.
        Assert.Single(repository.UpdatedSnapshots);
        AssertInvoiceMatches(repository.UpdatedSnapshots[0], stored);
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
        AssertInvoiceMatches(before, repository.GetById("INV-1")!);
    }
}
