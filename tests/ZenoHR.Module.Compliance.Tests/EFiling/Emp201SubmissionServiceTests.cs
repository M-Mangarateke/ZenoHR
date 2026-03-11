// CTL-SARS-010: Tests for Emp201SubmissionService — EMP201 eFiling submission workflow.
// TASK-131: Validates input handling, delegation to IEFilingClient, and status queries.

using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Services.EFiling;

namespace ZenoHR.Module.Compliance.Tests.EFiling;

public sealed class Emp201SubmissionServiceTests
{
    private readonly IEFilingClient _eFilingClient = Substitute.For<IEFilingClient>();
    private readonly ILogger<Emp201SubmissionService> _logger = Substitute.For<ILogger<Emp201SubmissionService>>();
    private readonly Emp201SubmissionService _service;

    public Emp201SubmissionServiceTests()
    {
        _service = new Emp201SubmissionService(_eFilingClient, _logger);
    }

    private static readonly byte[] ValidContent = [0x45, 0x4D, 0x50]; // "EMP"

    // ── SubmitEmp201Async — success ─────────────────────────────────────────

    [Fact]
    public async Task SubmitEmp201Async_ValidInputs_ReturnsSuccessAndDelegatesToClient()
    {
        // Arrange
        var expectedResult = new EFilingSubmissionResult(
            SubmissionId: "SUB-001",
            Status: EFilingSubmissionStatus.Submitted,
            SubmittedAt: DateTimeOffset.UtcNow,
            SarsReferenceNumber: null,
            ErrorMessage: null,
            RetryCount: 0);

        _eFilingClient
            .SubmitAsync(Arg.Any<EFilingSubmissionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EFilingSubmissionResult>.Success(expectedResult));

        // Act
        var result = await _service.SubmitEmp201Async(
            "tenant-1", 2026, 3, ValidContent, "user-001", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubmissionId.Should().Be("SUB-001");
        result.Value.Status.Should().Be(EFilingSubmissionStatus.Submitted);

        await _eFilingClient.Received(1).SubmitAsync(
            Arg.Is<EFilingSubmissionRequest>(r =>
                r.TenantId == "tenant-1" &&
                r.SubmissionType == EFilingSubmissionType.EMP201 &&
                r.TaxYear == 2026 &&
                r.TaxPeriod == 3 &&
                r.FileName == "EMP201_2026_03_tenant-1.csv" &&
                r.SubmittedBy == "user-001"),
            Arg.Any<CancellationToken>());
    }

    // ── SubmitEmp201Async — validation failures ─────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SubmitEmp201Async_EmptyTenantId_ReturnsFailure(string? tenantId)
    {
        var result = await _service.SubmitEmp201Async(
            tenantId!, 2026, 3, ValidContent, "user-001", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
        result.Error.Message.Should().Contain("TenantId");
    }

    [Fact]
    public async Task SubmitEmp201Async_NullContent_ReturnsFailure()
    {
        var result = await _service.SubmitEmp201Async(
            "tenant-1", 2026, 3, null!, "user-001", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
        result.Error.Message.Should().Contain("content");
    }

    [Fact]
    public async Task SubmitEmp201Async_EmptyContent_ReturnsFailure()
    {
        var result = await _service.SubmitEmp201Async(
            "tenant-1", 2026, 3, [], "user-001", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
        result.Error.Message.Should().Contain("content");
    }

    [Theory]
    [InlineData(2019)]
    [InlineData(2100)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SubmitEmp201Async_InvalidTaxYear_ReturnsFailure(int taxYear)
    {
        var result = await _service.SubmitEmp201Async(
            "tenant-1", taxYear, 3, ValidContent, "user-001", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
        result.Error.Message.Should().Contain("Tax year");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    [InlineData(99)]
    public async Task SubmitEmp201Async_InvalidTaxPeriod_ReturnsFailure(int taxPeriod)
    {
        var result = await _service.SubmitEmp201Async(
            "tenant-1", 2026, taxPeriod, ValidContent, "user-001", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
        result.Error.Message.Should().Contain("Tax period");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SubmitEmp201Async_EmptySubmittedBy_ReturnsFailure(string? submittedBy)
    {
        var result = await _service.SubmitEmp201Async(
            "tenant-1", 2026, 3, ValidContent, submittedBy!, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
        result.Error.Message.Should().Contain("SubmittedBy");
    }

    [Fact]
    public async Task SubmitEmp201Async_ClientReturnsFailure_PropagatesError()
    {
        _eFilingClient
            .SubmitAsync(Arg.Any<EFilingSubmissionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.EFilingSubmissionFailed, "Connection refused"));

        var result = await _service.SubmitEmp201Async(
            "tenant-1", 2026, 3, ValidContent, "user-001", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.EFilingSubmissionFailed);
    }

    [Fact]
    public async Task SubmitEmp201Async_DoesNotCallClient_WhenValidationFails()
    {
        // Invalid tenant — should not reach the client
        await _service.SubmitEmp201Async(
            "", 2026, 3, ValidContent, "user-001", CancellationToken.None);

        await _eFilingClient.DidNotReceive()
            .SubmitAsync(Arg.Any<EFilingSubmissionRequest>(), Arg.Any<CancellationToken>());
    }

    // ── GetSubmissionStatusAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetSubmissionStatusAsync_ValidInputs_DelegatesToClient()
    {
        var expectedResult = new EFilingSubmissionResult(
            SubmissionId: "SUB-001",
            Status: EFilingSubmissionStatus.Accepted,
            SubmittedAt: DateTimeOffset.UtcNow,
            SarsReferenceNumber: "SARS-REF-123",
            ErrorMessage: null,
            RetryCount: 0);

        _eFilingClient
            .GetStatusAsync("SUB-001", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(Result<EFilingSubmissionResult>.Success(expectedResult));

        var result = await _service.GetSubmissionStatusAsync("SUB-001", "tenant-1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SubmissionId.Should().Be("SUB-001");
        result.Value.Status.Should().Be(EFilingSubmissionStatus.Accepted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSubmissionStatusAsync_EmptySubmissionId_ReturnsFailure(string? submissionId)
    {
        var result = await _service.GetSubmissionStatusAsync(submissionId!, "tenant-1", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
        result.Error.Message.Should().Contain("SubmissionId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSubmissionStatusAsync_EmptyTenantId_ReturnsFailure(string? tenantId)
    {
        var result = await _service.GetSubmissionStatusAsync("SUB-001", tenantId!, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
        result.Error.Message.Should().Contain("TenantId");
    }

    [Fact]
    public async Task GetSubmissionStatusAsync_DoesNotCallClient_WhenValidationFails()
    {
        await _service.GetSubmissionStatusAsync("", "tenant-1", CancellationToken.None);

        await _eFilingClient.DidNotReceive()
            .GetStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
