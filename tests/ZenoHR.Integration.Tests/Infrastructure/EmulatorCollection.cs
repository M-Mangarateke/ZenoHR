// REQ-OPS-003: xUnit collection definition — all integration tests share one fixture instance.
// Tests in [Collection(EmulatorCollection.Name)] share the FirestoreEmulatorFixture
// (one fixture per test run, not one per test class).

namespace ZenoHR.Integration.Tests.Infrastructure;

/// <summary>
/// xUnit collection definition that wires all integration tests to the shared
/// FirestoreEmulatorFixture. All integration test classes must annotate with:
///   [Collection(EmulatorCollection.Name)]
/// REQ-OPS-003
/// </summary>
[CollectionDefinition(Name)]
public sealed class EmulatorCollection : ICollectionFixture<FirestoreEmulatorFixture>
{
    public const string Name = "FirestoreEmulator";
    // No body needed — CollectionDefinition + ICollectionFixture is the wiring
}
