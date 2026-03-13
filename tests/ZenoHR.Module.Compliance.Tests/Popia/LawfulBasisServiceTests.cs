// CTL-POPIA-001: Tests for POPIA §11 lawful basis validation service.

using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests.Popia;

public sealed class LawfulBasisServiceTests
{
    private readonly LawfulBasisService _service = new();

    private const string TenantId = "tenant-1";
    private const string CreatedBy = "user-hr-001";
    private const string Description = "Payroll processing for salary calculations";

    private static ProcessingPurpose CreateTestPurpose(
        string tenantId = TenantId,
        LawfulBasis basis = LawfulBasis.LegalObligation,
        IReadOnlyList<string>? categories = null,
        bool isActive = true) => new()
    {
        PurposeId = "PUR-TEST-001",
        TenantId = tenantId,
        Description = Description,
        LawfulBasis = basis,
        DataCategories = categories ?? ["salary", "id_number", "banking"],
        CreatedBy = CreatedBy,
        CreatedAt = DateTimeOffset.UtcNow,
        IsActive = isActive,
    };

    // ── RegisterPurpose ────────────────────────────────────────────────────

    [Fact]
    public void RegisterPurpose_ValidInput_Succeeds()
    {
        // CTL-POPIA-001
        var result = _service.RegisterPurpose(
            TenantId, Description, LawfulBasis.Contract, ["salary", "banking"], CreatedBy);

        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be(TenantId);
        result.Value.Description.Should().Be(Description);
        result.Value.LawfulBasis.Should().Be(LawfulBasis.Contract);
        result.Value.DataCategories.Should().Contain("salary");
        result.Value.DataCategories.Should().Contain("banking");
        result.Value.CreatedBy.Should().Be(CreatedBy);
        result.Value.IsActive.Should().BeTrue();
        result.Value.PurposeId.Should().StartWith("PUR-");
    }

    [Theory]
    [InlineData("", "desc", "user")]
    [InlineData("  ", "desc", "user")]
    [InlineData(null, "desc", "user")]
    public void RegisterPurpose_EmptyTenantId_Rejected(string? tenantId, string desc, string user)
    {
        // CTL-POPIA-001
        var result = _service.RegisterPurpose(tenantId!, desc, LawfulBasis.Consent, ["data"], user);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
        result.Error.Message.Should().Contain("TenantId");
    }

    [Theory]
    [InlineData("tenant", "", "user")]
    [InlineData("tenant", "  ", "user")]
    [InlineData("tenant", null, "user")]
    public void RegisterPurpose_EmptyDescription_Rejected(string tenant, string? desc, string user)
    {
        // CTL-POPIA-001
        var result = _service.RegisterPurpose(tenant, desc!, LawfulBasis.Consent, ["data"], user);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
        result.Error.Message.Should().Contain("Description");
    }

    [Theory]
    [InlineData("tenant", "desc", "")]
    [InlineData("tenant", "desc", "  ")]
    [InlineData("tenant", "desc", null)]
    public void RegisterPurpose_EmptyCreatedBy_Rejected(string tenant, string desc, string? user)
    {
        // CTL-POPIA-001
        var result = _service.RegisterPurpose(tenant, desc, LawfulBasis.Consent, ["data"], user!);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
        result.Error.Message.Should().Contain("CreatedBy");
    }

    [Fact]
    public void RegisterPurpose_UnknownLawfulBasis_Rejected()
    {
        // CTL-POPIA-001
        var result = _service.RegisterPurpose(TenantId, Description, LawfulBasis.Unknown, ["data"], CreatedBy);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("LawfulBasis");
    }

    [Theory]
    [InlineData(LawfulBasis.Consent)]
    [InlineData(LawfulBasis.Contract)]
    [InlineData(LawfulBasis.LegalObligation)]
    [InlineData(LawfulBasis.LegitimateInterest)]
    [InlineData(LawfulBasis.VitalInterest)]
    [InlineData(LawfulBasis.PublicFunction)]
    public void RegisterPurpose_AllSixLawfulBasisTypes_Accepted(LawfulBasis basis)
    {
        // CTL-POPIA-001
        var result = _service.RegisterPurpose(TenantId, Description, basis, ["data"], CreatedBy);

        result.IsSuccess.Should().BeTrue();
        result.Value.LawfulBasis.Should().Be(basis);
    }

    // ── ValidateProcessingAllowed ──────────────────────────────────────────

    [Fact]
    public void ValidateProcessingAllowed_MatchingActivePurpose_ReturnsSuccess()
    {
        // CTL-POPIA-001
        var purposes = new List<ProcessingPurpose>
        {
            CreateTestPurpose(categories: ["salary", "id_number"]),
        };

        var result = _service.ValidateProcessingAllowed(TenantId, "salary", purposes);

        result.IsSuccess.Should().BeTrue();
        result.Value.DataCategories.Should().Contain("salary");
    }

    [Fact]
    public void ValidateProcessingAllowed_NoPurposeCoversCategory_ReturnsFailure()
    {
        // CTL-POPIA-001
        var purposes = new List<ProcessingPurpose>
        {
            CreateTestPurpose(categories: ["salary"]),
        };

        var result = _service.ValidateProcessingAllowed(TenantId, "medical_records", purposes);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ComplianceCheckFailed);
        result.Error.Message.Should().Contain("medical_records");
    }

    [Fact]
    public void ValidateProcessingAllowed_IgnoresInactivePurposes()
    {
        // CTL-POPIA-001
        var purposes = new List<ProcessingPurpose>
        {
            CreateTestPurpose(categories: ["salary"], isActive: false),
        };

        var result = _service.ValidateProcessingAllowed(TenantId, "salary", purposes);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ComplianceCheckFailed);
    }

    [Fact]
    public void ValidateProcessingAllowed_IgnoresDifferentTenant()
    {
        // CTL-POPIA-001
        var purposes = new List<ProcessingPurpose>
        {
            CreateTestPurpose(tenantId: "other-tenant", categories: ["salary"]),
        };

        var result = _service.ValidateProcessingAllowed(TenantId, "salary", purposes);

        result.IsFailure.Should().BeTrue();
    }

    // ── GetActivePurposes ──────────────────────────────────────────────────

    [Fact]
    public void GetActivePurposes_FiltersByTenantAndActiveStatus()
    {
        // CTL-POPIA-001
        var purposes = new List<ProcessingPurpose>
        {
            CreateTestPurpose(tenantId: TenantId, isActive: true),
            CreateTestPurpose(tenantId: TenantId, isActive: false),
            CreateTestPurpose(tenantId: "other-tenant", isActive: true),
        };

        var result = _service.GetActivePurposes(TenantId, purposes);

        result.Should().HaveCount(1);
        result[0].TenantId.Should().Be(TenantId);
        result[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public void GetActivePurposes_EmptyList_ReturnsEmpty()
    {
        // CTL-POPIA-001
        var result = _service.GetActivePurposes(TenantId, []);

        result.Should().BeEmpty();
    }

    // ── RevokePurpose ──────────────────────────────────────────────────────

    [Fact]
    public void RevokePurpose_ActivePurpose_MarksInactive()
    {
        // CTL-POPIA-001
        var purpose = CreateTestPurpose(isActive: true);

        var result = _service.RevokePurpose(purpose, "user-admin-001");

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        result.Value.RevokedBy.Should().Be("user-admin-001");
        result.Value.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public void RevokePurpose_AlreadyRevoked_ReturnsFailure()
    {
        // CTL-POPIA-001
        var purpose = CreateTestPurpose(isActive: false);

        var result = _service.RevokePurpose(purpose, "user-admin-001");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
    }

    [Fact]
    public void RevokePurpose_EmptyRevokedBy_Rejected()
    {
        // CTL-POPIA-001
        var purpose = CreateTestPurpose(isActive: true);

        var result = _service.RevokePurpose(purpose, "");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }
}
