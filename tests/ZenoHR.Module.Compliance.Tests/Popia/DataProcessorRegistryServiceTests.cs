// CTL-POPIA-013, CTL-POPIA-014: Tests for data processor registry and cross-border transfer governance.

using FluentAssertions;
using ZenoHR.Module.Compliance.Models;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests.Popia;

public sealed class DataProcessorRegistryServiceTests
{
    private readonly DataProcessorRegistryService _service = new();

    private static readonly IReadOnlySet<string> SaRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "africa-south1",
        "South Africa North"
    };

    private static DataProcessor CreateTestProcessor(
        string? processorId = null,
        string? name = null,
        string? serviceDescription = null,
        IReadOnlyList<string>? dataTypes = null,
        string? region = null,
        DpaStatus dpaStatus = DpaStatus.Obtained,
        bool isSubProcessor = false)
    {
        return new DataProcessor
        {
            ProcessorId = processorId ?? "PROC-001",
            Name = name ?? "Test Processor",
            ServiceDescription = serviceDescription ?? "Test service",
            DataTypesProcessed = dataTypes ?? ["Email", "Name"],
            Region = region ?? "South Africa North",
            DpaStatus = dpaStatus,
            IsSubProcessor = isSubProcessor,
            LastReviewedAt = DateTimeOffset.UtcNow
        };
    }

    // ── RegisterProcessor ─────────────────────────────────────────────────

    [Fact]
    public void RegisterProcessor_ValidData_Succeeds()
    {
        var processor = CreateTestProcessor();

        var result = _service.RegisterProcessor(processor);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProcessorId.Should().Be("PROC-001");
        result.Value.Name.Should().Be("Test Processor");
    }

    [Fact]
    public void RegisterProcessor_EmptyName_ReturnsFailure()
    {
        var processor = CreateTestProcessor(name: "");

        var result = _service.RegisterProcessor(processor);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Name");
    }

    [Fact]
    public void RegisterProcessor_EmptyServiceDescription_ReturnsFailure()
    {
        var processor = CreateTestProcessor(serviceDescription: "");

        var result = _service.RegisterProcessor(processor);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("ServiceDescription");
    }

    [Fact]
    public void RegisterProcessor_EmptyProcessorId_ReturnsFailure()
    {
        var processor = CreateTestProcessor(processorId: "");

        var result = _service.RegisterProcessor(processor);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("ProcessorId");
    }

    [Fact]
    public void RegisterProcessor_EmptyDataTypes_ReturnsFailure()
    {
        var processor = CreateTestProcessor(dataTypes: []);

        var result = _service.RegisterProcessor(processor);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("data type");
    }

    [Fact]
    public void RegisterProcessor_EmptyRegion_ReturnsFailure()
    {
        var processor = CreateTestProcessor(region: "");

        var result = _service.RegisterProcessor(processor);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Region");
    }

    [Fact]
    public void RegisterProcessor_UnknownDpaStatus_ReturnsFailure()
    {
        var processor = CreateTestProcessor(dpaStatus: DpaStatus.Unknown);

        var result = _service.RegisterProcessor(processor);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("DpaStatus");
    }

    // ── GetProcessorsRequiringDpa ─────────────────────────────────────────

    [Fact]
    public void GetProcessorsRequiringDpa_ReturnsOnlyRequiredStatus()
    {
        var processors = new List<DataProcessor>
        {
            CreateTestProcessor(processorId: "P1", dpaStatus: DpaStatus.Required),
            CreateTestProcessor(processorId: "P2", dpaStatus: DpaStatus.Obtained),
            CreateTestProcessor(processorId: "P3", dpaStatus: DpaStatus.NotRequired),
            CreateTestProcessor(processorId: "P4", dpaStatus: DpaStatus.Required),
            CreateTestProcessor(processorId: "P5", dpaStatus: DpaStatus.Expired)
        };

        var result = _service.GetProcessorsRequiringDpa(processors);

        result.Should().HaveCount(2);
        result.Select(p => p.ProcessorId).Should().BeEquivalentTo(["P1", "P4"]);
    }

    [Fact]
    public void GetProcessorsRequiringDpa_NoneRequired_ReturnsEmpty()
    {
        var processors = new List<DataProcessor>
        {
            CreateTestProcessor(processorId: "P1", dpaStatus: DpaStatus.Obtained),
            CreateTestProcessor(processorId: "P2", dpaStatus: DpaStatus.NotRequired)
        };

        var result = _service.GetProcessorsRequiringDpa(processors);

        result.Should().BeEmpty();
    }

    // ── GetCrossBorderProcessors ──────────────────────────────────────────

    [Fact]
    public void GetCrossBorderProcessors_IdentifiesNonSaProcessors()
    {
        var processors = new List<DataProcessor>
        {
            CreateTestProcessor(processorId: "P1", name: "SA Firestore", region: "africa-south1"),
            CreateTestProcessor(processorId: "P2", name: "SA Azure", region: "South Africa North"),
            CreateTestProcessor(processorId: "P3", name: "Firebase Auth", region: "Multi-region (user metadata)"),
            CreateTestProcessor(processorId: "P4", name: "EU Service", region: "europe-west1")
        };

        var result = _service.GetCrossBorderProcessors(processors, SaRegions);

        result.Should().HaveCount(2);
        result.Select(p => p.ProcessorId).Should().BeEquivalentTo(["P3", "P4"]);
    }

    [Fact]
    public void GetCrossBorderProcessors_AllInSa_ReturnsEmpty()
    {
        var processors = new List<DataProcessor>
        {
            CreateTestProcessor(processorId: "P1", region: "africa-south1"),
            CreateTestProcessor(processorId: "P2", region: "South Africa North")
        };

        var result = _service.GetCrossBorderProcessors(processors, SaRegions);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetCrossBorderProcessors_ExcludesInProcessLibraries()
    {
        var processors = new List<DataProcessor>
        {
            CreateTestProcessor(processorId: "P1", region: "N/A (in-process, no data transfer)"),
            CreateTestProcessor(processorId: "P2", region: "N/A (in-process)"),
            CreateTestProcessor(processorId: "P3", region: "N/A"),
            CreateTestProcessor(processorId: "P4", region: "us-east1")
        };

        var result = _service.GetCrossBorderProcessors(processors, SaRegions);

        result.Should().HaveCount(1);
        result[0].ProcessorId.Should().Be("P4");
    }

    [Fact]
    public void GetCrossBorderProcessors_CaseInsensitiveRegionMatching()
    {
        var processors = new List<DataProcessor>
        {
            CreateTestProcessor(processorId: "P1", region: "AFRICA-SOUTH1"),
            CreateTestProcessor(processorId: "P2", region: "south africa north")
        };

        var result = _service.GetCrossBorderProcessors(processors, SaRegions);

        result.Should().BeEmpty();
    }

    // ── ValidateAllDpasObtained ───────────────────────────────────────────

    [Fact]
    public void ValidateAllDpasObtained_AllObtained_ReturnsTrue()
    {
        var processors = new List<DataProcessor>
        {
            CreateTestProcessor(processorId: "P1", dpaStatus: DpaStatus.Obtained),
            CreateTestProcessor(processorId: "P2", dpaStatus: DpaStatus.Obtained)
        };

        var result = _service.ValidateAllDpasObtained(processors);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void ValidateAllDpasObtained_AnyStillRequired_ReturnsFalse()
    {
        var processors = new List<DataProcessor>
        {
            CreateTestProcessor(processorId: "P1", name: "Google Cloud", dpaStatus: DpaStatus.Obtained),
            CreateTestProcessor(processorId: "P2", name: "Pending Vendor", dpaStatus: DpaStatus.Required)
        };

        var result = _service.ValidateAllDpasObtained(processors);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Pending Vendor");
        result.Error.Message.Should().Contain("Required");
    }

    [Fact]
    public void ValidateAllDpasObtained_NotRequiredProcessorsExcluded()
    {
        var processors = new List<DataProcessor>
        {
            CreateTestProcessor(processorId: "P1", dpaStatus: DpaStatus.Obtained),
            CreateTestProcessor(processorId: "P2", dpaStatus: DpaStatus.NotRequired)
        };

        var result = _service.ValidateAllDpasObtained(processors);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void ValidateAllDpasObtained_ExpiredDpaTreatedAsNonCompliant()
    {
        var processors = new List<DataProcessor>
        {
            CreateTestProcessor(processorId: "P1", name: "Active Vendor", dpaStatus: DpaStatus.Obtained),
            CreateTestProcessor(processorId: "P2", name: "Expired Vendor", dpaStatus: DpaStatus.Expired)
        };

        var result = _service.ValidateAllDpasObtained(processors);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Expired Vendor");
        result.Error.Message.Should().Contain("Expired");
    }

    [Fact]
    public void ValidateAllDpasObtained_EmptyList_ReturnsTrue()
    {
        var result = _service.ValidateAllDpasObtained([]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void ValidateAllDpasObtained_MultipleNonCompliant_ListsAll()
    {
        var processors = new List<DataProcessor>
        {
            CreateTestProcessor(processorId: "P1", name: "Vendor A", dpaStatus: DpaStatus.Required),
            CreateTestProcessor(processorId: "P2", name: "Vendor B", dpaStatus: DpaStatus.Expired),
            CreateTestProcessor(processorId: "P3", name: "Vendor C", dpaStatus: DpaStatus.Obtained)
        };

        var result = _service.ValidateAllDpasObtained(processors);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("2 processor(s)");
        result.Error.Message.Should().Contain("Vendor A");
        result.Error.Message.Should().Contain("Vendor B");
    }
}
