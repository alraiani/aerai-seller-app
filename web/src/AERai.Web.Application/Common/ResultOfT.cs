namespace AERai.Web.Application.Common;

/// <summary>
/// A <see cref="Result"/> that carries a value when successful.
/// </summary>
/// <typeparam name="T">The type of the value produced on success.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, string error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>The value produced by a successful operation.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public T Value => IsSuccess
        ? _value!   // Non-null by construction: Success<T> is the only path that sets IsSuccess.
        : throw new InvalidOperationException("Cannot read the value of a failed result.");
}
