// REQ-OPS-001: Benchmark entry point.
// Usage:
//   dotnet run --project tests/ZenoHR.Benchmarks -c Release              → BenchmarkDotNet suite
//   dotnet run --project tests/ZenoHR.Benchmarks -- load-test            → SLA harness (500 employees)
//   dotnet run --project tests/ZenoHR.Benchmarks -- load-test 100        → SLA harness (100 employees)

using BenchmarkDotNet.Running;
using ZenoHR.Benchmarks;

if (args.Length > 0 && args[0].Equals("load-test", StringComparison.OrdinalIgnoreCase))
{
    int count = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 500;
    int exitCode = LoadTestHarness.RunSlaValidation(employeeCount: count);
    return exitCode;
}

// Default: run BenchmarkDotNet suite
BenchmarkRunner.Run<PayrollCalculationBenchmarks>();
return 0;
