// TC-OPS-001: Result<T> unit tests — verifies the discriminated union, combinators, and implicit conversions.

using FluentAssertions;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Domain.Tests.Errors;

public sealed class ResultTests
{
    // ── Result<T> — Success path ──────────────────────────────────────────────

    [Fact]
    public void Success_IsSuccessTrue_IsFailureFalse()
    {
        var result = Result<int>.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void Success_ValueReturnsWrappedValue()
    {
        var result = Result<string>.Success("hello");
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Success_AccessingError_Throws()
    {
        var result = Result<int>.Success(1);
        var act = () => result.Error;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot access Error*");
    }

    [Fact]
    public void Success_GetValueOrDefault_ReturnsValue()
    {
        var result = Result<int>.Success(99);
        result.GetValueOrDefault(0).Should().Be(99);
    }

    // ── Result<T> — Failure path ──────────────────────────────────────────────

    [Fact]
    public void Failure_IsSuccessFalse_IsFailureTrue()
    {
        var error = ZenoHrError.NotFound(ZenoHrErrorCode.EmployeeNotFound, "Employee", "emp-001");
        var result = Result<string>.Failure(error);
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Failure_ErrorReturnsWrappedError()
    {
        var error = new ZenoHrError(ZenoHrErrorCode.ValidationFailed, "Name is required", "Name");
        var result = Result<int>.Failure(error);
        result.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Failure_AccessingValue_Throws()
    {
        var result = Result<int>.Failure(ZenoHrErrorCode.Unknown, "something went wrong");
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot access Value*");
    }

    [Fact]
    public void Failure_GetValueOrDefault_ReturnsDefault()
    {
        var result = Result<int>.Failure(ZenoHrErrorCode.EmployeeNotFound, "not found");
        result.GetValueOrDefault(-1).Should().Be(-1);
    }

    [Fact]
    public void Failure_WithCodeAndMessage_SetsCodeAndMessage()
    {
        var result = Result<decimal>.Failure(ZenoHrErrorCode.ValidationFailed, "Amount must be positive");
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Be("Amount must be positive");
    }

    // ── Combinators — Map ─────────────────────────────────────────────────────

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        var result = Result<int>.Success(5).Map(x => x * 2);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10);
    }

    [Fact]
    public void Map_OnFailure_PropagatesError()
    {
        var error = new ZenoHrError(ZenoHrErrorCode.Unknown, "oops");
        var result = Result<int>.Failure(error).Map(x => x * 2);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
    }

    // ── Combinators — Bind ────────────────────────────────────────────────────

    [Fact]
    public void Bind_OnSuccess_ChainsNextResult()
    {
        var result = Result<int>.Success(10)
            .Bind(x => x > 5
                ? Result<string>.Success("large")
                : Result<string>.Failure(ZenoHrErrorCode.ValueOutOfRange, "too small"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("large");
    }

    [Fact]
    public void Bind_OnSuccess_NextFails_PropagatesNextError()
    {
        var result = Result<int>.Success(3)
            .Bind(x => x > 5
                ? Result<string>.Success("large")
                : Result<string>.Failure(ZenoHrErrorCode.ValueOutOfRange, "too small"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValueOutOfRange);
    }

    [Fact]
    public void Bind_OnFailure_ShortCircuits()
    {
        var firstError = new ZenoHrError(ZenoHrErrorCode.EmployeeNotFound, "not found");
        var callCount = 0;

        var result = Result<int>.Failure(firstError)
            .Bind(x => { callCount++; return Result<int>.Success(x + 1); });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(firstError);
        callCount.Should().Be(0, "next delegate must not be called on failure");
    }

    // ── Combinators — BindAsync ───────────────────────────────────────────────

    [Fact]
    public async Task BindAsync_OnSuccess_ChainsAsyncResult()
    {
        var result = await Result<int>.Success(7)
            .BindAsync(x => Task.FromResult(Result<int>.Success(x + 1)));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(8);
    }

    [Fact]
    public async Task BindAsync_OnFailure_ShortCircuits()
    {
        var error = new ZenoHrError(ZenoHrErrorCode.Unknown, "fail");
        var called = false;

        var result = await Result<int>.Failure(error)
            .BindAsync(x => { called = true; return Task.FromResult(Result<int>.Success(x)); });

        result.IsFailure.Should().BeTrue();
        called.Should().BeFalse();
    }

    // ── Side effects — OnSuccess / OnFailure ──────────────────────────────────

    [Fact]
    public void OnSuccess_OnSuccess_ExecutesAction()
    {
        var executed = false;
        Result<int>.Success(1).OnSuccess(_ => executed = true);
        executed.Should().BeTrue();
    }

    [Fact]
    public void OnSuccess_OnFailure_DoesNotExecute()
    {
        var executed = false;
        Result<int>.Failure(ZenoHrErrorCode.Unknown, "x").OnSuccess(_ => executed = true);
        executed.Should().BeFalse();
    }

    [Fact]
    public void OnFailure_OnFailure_ExecutesAction()
    {
        var executed = false;
        Result<int>.Failure(ZenoHrErrorCode.Unknown, "x").OnFailure(_ => executed = true);
        executed.Should().BeTrue();
    }

    [Fact]
    public void OnFailure_OnSuccess_DoesNotExecute()
    {
        var executed = false;
        Result<int>.Success(1).OnFailure(_ => executed = true);
        executed.Should().BeFalse();
    }

    [Fact]
    public void OnSuccess_ReturnsOriginalResult_ForChaining()
    {
        var result = Result<int>.Success(42).OnSuccess(_ => { });
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    // ── Implicit conversions ──────────────────────────────────────────────────

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        Result<int> result = 100;
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(100);
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        var error = new ZenoHrError(ZenoHrErrorCode.Forbidden, "forbidden");
        Result<string> result = error;
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
    }

    // ── ToString ──────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_Success_ContainsValue()
    {
        Result<int>.Success(7).ToString().Should().Contain("Success");
    }

    [Fact]
    public void ToString_Failure_ContainsFailure()
    {
        Result<int>.Failure(ZenoHrErrorCode.Unknown, "bad").ToString().Should().Contain("Failure");
    }

    // ── Non-generic Result ────────────────────────────────────────────────────

    [Fact]
    public void NonGeneric_Success_IsSuccessTrue()
    {
        var result = Result.Success();
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void NonGeneric_Failure_IsFailureTrue()
    {
        var result = Result.Failure(ZenoHrErrorCode.Unknown, "something failed");
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.Unknown);
    }

    [Fact]
    public void NonGeneric_Success_AccessingError_Throws()
    {
        var result = Result.Success();
        var act = () => result.Error;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NonGeneric_Success_IsSingleton()
    {
        // Two calls to Success() should return the same cached instance.
        var a = Result.Success();
        var b = Result.Success();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void NonGeneric_ToTyped_OnSuccess_WrapsValue()
    {
        var result = Result.Success().ToTyped(() => 42);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void NonGeneric_ToTyped_OnFailure_PropagatesError()
    {
        var error = new ZenoHrError(ZenoHrErrorCode.FirestoreUnavailable, "db down");
        var result = Result.Failure(error).ToTyped<int>(() => 0);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void NonGeneric_ImplicitConversion_FromError_CreatesFailure()
    {
        var error = new ZenoHrError(ZenoHrErrorCode.Unauthorized, "not authenticated");
        Result result = error;
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.Unauthorized);
    }

    // ── ZenoHrError factory methods ───────────────────────────────────────────

    [Fact]
    public void ZenoHrError_NotFound_ContainsEntityAndId()
    {
        var err = ZenoHrError.NotFound(ZenoHrErrorCode.EmployeeNotFound, "Employee", "emp-001");
        err.Code.Should().Be(ZenoHrErrorCode.EmployeeNotFound);
        err.Message.Should().Contain("emp-001");
    }

    [Fact]
    public void ZenoHrError_ValidationFailed_SetsPropertyName()
    {
        var err = ZenoHrError.ValidationFailed("Salary", "Salary must be positive", 0m);
        err.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        err.PropertyName.Should().Be("Salary");
        err.AttemptedValue.Should().Be(0m);
    }

    [Fact]
    public void ZenoHrError_Forbidden_HasForbiddenCode()
    {
        var err = ZenoHrError.Forbidden("Payroll access denied");
        err.Code.Should().Be(ZenoHrErrorCode.Forbidden);
        err.Message.Should().Contain("Payroll access denied");
    }

    [Fact]
    public void ZenoHrError_HashChainBroken_ContainsDetail()
    {
        var err = ZenoHrError.HashChainBroken("Event #42 hash mismatch");
        err.Code.Should().Be(ZenoHrErrorCode.HashChainBroken);
        err.Message.Should().Contain("Event #42 hash mismatch");
    }

    [Fact]
    public void ZenoHrError_EmptyMessage_Throws()
    {
        var act = () => new ZenoHrError(ZenoHrErrorCode.Unknown, "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ZenoHrError_ToString_WithPropertyName_IncludesProperty()
    {
        var err = new ZenoHrError(ZenoHrErrorCode.ValidationFailed, "Invalid", "Email");
        err.ToString().Should().Contain("Email");
    }

    [Fact]
    public void ZenoHrError_ToString_WithoutPropertyName_OmitsProperty()
    {
        var err = new ZenoHrError(ZenoHrErrorCode.Unknown, "Generic error");
        err.ToString().Should().NotContain(":");
    }
}
