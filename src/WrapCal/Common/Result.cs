namespace WrapCal.Common;

/// <summary>A machine-readable failure reason plus a message suitable for showing a user.</summary>
public readonly record struct Error(string Code, string Message);

/// <summary>
/// An outcome that can fail for expected reasons — invalid input, a box too large for the roll.
/// Expected failures are values; exceptions stay reserved for programmer error.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T? value, Error? error)
    {
        _value = value;
        _error = error;
    }

    public bool IsSuccess => _error is null;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"A failed result carries no value: {_error!.Value.Message}");

    public Error Error => IsSuccess
        ? throw new InvalidOperationException("A successful result carries no error.")
        : _error!.Value;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(string code, string message) => new(default, new Error(code, message));
}
