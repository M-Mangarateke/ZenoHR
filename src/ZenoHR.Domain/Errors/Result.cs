// REQ-OPS-001: Result<T> — discriminated union for operations that can fail.
// No exceptions for business logic failures — always return Result.
// Infrastructure exceptions (Firestore unavailable, network) propagate naturally.

namespace ZenoHR.Domain.Errors;

/// <summary>
/// Discriminated union representing either a successful value of type <typeparamref name="T"/>
/// or a <see cref="ZenoHrError"/> describing the failure.
/// <para>
/// Business logic must NEVER throw exceptions for expected failures.
/// Use <see cref="Result{T}.Failure(ZenoHrError)"/> instead.
/// </para>
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly ZenoHrError? _error;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    private Result(ZenoHrError error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    /// <summary>The success value. Throws <see cref="InvalidOperationException"/> if this is a failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value on a failed Result. Error: {_error}");

    /// <summary>The failure error. Throws <see cref="InvalidOperationException"/> if this is a success.</summary>
    public ZenoHrError Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful Result.");

    // ── Factory methods ──────────────────────────────────────────────────────

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(ZenoHrError error) => new(error);

    public static Result<T> Failure(ZenoHrErrorCode code, string message) =>
        new(new ZenoHrError(code, message));

    // ── Functional combinators ───────────────────────────────────────────────

    /// <summary>Transform the success value. Propagates failure unchanged.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper) =>
        IsSuccess ? Result<TOut>.Success(mapper(Value)) : Result<TOut>.Failure(Error);

    /// <summary>Chain result-returning operations. Propagates the first failure.</summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> next) =>
        IsSuccess ? next(Value) : Result<TOut>.Failure(Error);

    /// <summary>Async chain result-returning operations.</summary>
    public async Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> next) =>
        IsSuccess ? await next(Value) : Result<TOut>.Failure(Error);

    /// <summary>Execute side-effect on success; return this unchanged.</summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess) action(Value);
        return this;
    }

    /// <summary>Execute side-effect on failure; return this unchanged.</summary>
    public Result<T> OnFailure(Action<ZenoHrError> action)
    {
        if (IsFailure) action(Error);
        return this;
    }

    /// <summary>Unwrap to <typeparamref name="T"/> or return a default on failure.</summary>
    public T GetValueOrDefault(T defaultValue = default!) =>
        IsSuccess ? Value : defaultValue;

    // ── Implicit conversions ─────────────────────────────────────────────────

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(ZenoHrError error) => Failure(error);

    public override string ToString() =>
        IsSuccess ? $"Success({Value})" : $"Failure({Error})";
}

/// <summary>
/// Non-generic Result for operations that succeed with no value (void equivalent).
/// </summary>
public sealed class Result
{
    private static readonly Result _success = new(true, null);

    private readonly bool _isSuccess;
    private readonly ZenoHrError? _error;

    private Result(bool isSuccess, ZenoHrError? error)
    {
        _isSuccess = isSuccess;
        _error = error;
    }

    public bool IsSuccess => _isSuccess;
    public bool IsFailure => !_isSuccess;

    public ZenoHrError Error => !_isSuccess
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful Result.");

    public static Result Success() => _success;

    public static Result Failure(ZenoHrError error) => new(false, error);

    public static Result Failure(ZenoHrErrorCode code, string message) =>
        Failure(new ZenoHrError(code, message));

    /// <summary>Convert to a typed Result carrying a value, if successful.</summary>
    public Result<T> ToTyped<T>(Func<T> valueFactory) =>
        IsSuccess ? Result<T>.Success(valueFactory()) : Result<T>.Failure(Error);

    public static implicit operator Result(ZenoHrError error) => Failure(error);

    public override string ToString() =>
        IsSuccess ? "Success" : $"Failure({Error})";
}
