using System.Diagnostics.CodeAnalysis;

namespace VehicleGo.Domain.Common;

/// <summary>A domain error: a fixed code plus, optionally, the (camelCase JSON) field it refers to.</summary>
public sealed record Error(ErrorCode Code, string? Field = null);

/// <summary>Success, or failure with an <see cref="Common.Error"/>; used instead of throwing.</summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static Result Failure(ErrorCode code, string? field = null) => new(new Error(code, field));

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
}

/// <summary>Success with a value, or failure with an <see cref="Common.Error"/>.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error) : base(error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result is a failure ({Error.Code}); it has no value.");

    public static Result<T> Success(T value) => new(value, null);

    public static new Result<T> Failure(Error error) => new(default, error ?? throw new ArgumentNullException(nameof(error)));

    public static new Result<T> Failure(ErrorCode code, string? field = null) => new(default, new Error(code, field));
}
