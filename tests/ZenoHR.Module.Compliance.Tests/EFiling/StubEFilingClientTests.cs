// CTL-SARS-010: Tests for the stub SARS eFiling client.

using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Services.EFiling;

namespace ZenoHR.Module.Compliance.Tests.EFiling;

public sealed class StubEFilingClientTests
{
    private readonly ILogger<StubEFilingClient> _logger = Substitute.For<ILogger<StubEFilingClient>>();
    private readonly StubEFilingClient _client;

    public StubEFilingClientTests()
    {
        _client = new StubEFilingClient(_logger);
    }

    private static EFilingSubmissionRequest CreateValidRequest(
        EFilingSubmissionType type = EFilingSubmissionType.EMP201) =>
        new(
            TenantId: "tenant-1",
            SubmissionType: type,
            TaxYear: 2026,
            TaxPeriod: 3,
            FileContent: [0x01, 0x02, 0x03],
            FileName: "EMP201_202603.csv",
            SubmittedBy: "user-001");

    // ── SubmitAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAsync_ValidRequest_ReturnsSuccessWithSubmissionId()
    {
        var request = CreateValidRequest();

        var result = await _client.SubmitAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SubmissionId.Should().StartWith("STUB-");
        result.Value.Status.Should().Be(EFilingSubmissionStatus.Submitted);
        result.Value.SubmittedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        result.Value.ErrorMessage.Should().BeNull();
        result.Value.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task SubmitAsync_NullTenantId_ReturnsFailure()
    {
        var request = CreateValidRequest() with { TenantId = null! };

        var result = await _client.SubmitAsync(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public async Task SubmitAsync_EmptyTenantId_ReturnsFailure()
    {
        var request = CreateValidRequest() with { TenantId = "" };

        var result = await _client.SubmitAsync(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public async Task SubmitAsync_WhitespaceTenantId_ReturnsFailure()
    {
        var request = CreateValidRequest() with { TenantId = "   " };

        var result = await _client.SubmitAsync(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public async Task SubmitAsync_UnknownSubmissionType_ReturnsFailure()
    {
        var request = CreateValidRequest() with { SubmissionType = EFilingSubmissionType.Unknown };

        var result = await _client.SubmitAsync(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
    }

    [Theory]
    [InlineData(EFilingSubmissionType.EMP201)]
    [InlineData(EFilingSubmissionType.EMP501)]
    [InlineData(EFilingSubmissionType.IRP5IT3a)]
    [InlineData(EFilingSubmissionType.EMP601)]
    [InlineData(EFilingSubmissionType.EMP701)]
    public async Task SubmitAsync_AllSubmissionTypes_ReturnsSuccess(EFilingSubmissionType type)
    {
        var request = CreateValidRequest(type);

        var result = await _client.SubmitAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(EFilingSubmissionStatus.Submitted);
    }

    [Fact]
    public async Task SubmitAsync_GeneratesUniqueSubmissionIds()
    {
        var request = CreateValidRequest();

        var result1 = await _client.SubmitAsync(request, CancellationToken.None);
        var result2 = await _client.SubmitAsync(request, CancellationToken.None);

        result1.Value.SubmissionId.Should().NotBe(result2.Value.SubmissionId);
    }

    // ── GetStatusAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_KnownSubmission_ReturnsAccepted()
    {
        var request = CreateValidRequest();
        var submitResult = await _client.SubmitAsync(request, CancellationToken.None);
        var submissionId = submitResult.Value.SubmissionId;

        var statusResult = await _client.GetStatusAsync(submissionId, "tenant-1", CancellationToken.None);

        statusResult.IsSuccess.Should().BeTrue();
        statusResult.Value.SubmissionId.Should().Be(submissionId);
        statusResult.Value.Status.Should().Be(EFilingSubmissionStatus.Accepted);
    }

    [Fact]
    public async Task GetStatusAsync_UnknownSubmission_ReturnsFailure()
    {
        var result = await _client.GetStatusAsync("NONEXISTENT-ID", "tenant-1", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ComplianceSubmissionNotFound);
    }

    [Fact]
    public async Task GetStatusAsync_EmptyTenantId_ReturnsFailure()
    {
        var result = await _client.GetStatusAsync("some-id", "", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public async Task GetStatusAsync_EmptySubmissionId_ReturnsFailure()
    {
        var result = await _client.GetStatusAsync("", "tenant-1", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    // ── GetSubmissionHistoryAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetSubmissionHistoryAsync_NewTenant_ReturnsEmptyList()
    {
        var result = await _client.GetSubmissionHistoryAsync("tenant-new", 2026, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSubmissionHistoryAsync_EmptyTenantId_ReturnsFailure()
    {
        var result = await _client.GetSubmissionHistoryAsync("", 2026, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public async Task GetSubmissionHistoryAsync_NullTenantId_ReturnsFailure()
    {
        var result = await _client.GetSubmissionHistoryAsync(null!, 2026, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }
}
