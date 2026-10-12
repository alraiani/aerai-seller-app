namespace AERai.Web.Application.Common;

/// <summary>
/// Outcome of an operation whose failure is expected and user-correctable (validation errors,
/// "not found", bad credentials). Unexpected failures are exceptions, not results.
/// </summary>
public class Result
{
    /// <summary>Initializes a result. Use <see cref="Success()"/> or <see cref="Failure(string)"/> instead.</summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="error">Failure message; must be empty on success and non-empty on failure.</param>
    protected Result(bool isSuccess, string error)
    {
        if (isSuccess == !string.IsNullOrEmpty(error))
        {
            throw new ArgumentException("A successful result cannot have an error, and a failure must have one.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>A user-presentable failure message; empty on success.</summary>
    public string Error { get; }

    /// <summary>Creates a successful result.</summary>
    /// <returns>A successful <see cref="Result"/>.</returns>
    public static Result Success() => new(true, string.Empty);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">A user-presentable message describing what went wrong.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    public static Result Failure(string error) => new(false, error);

    /// <summary>Creates a successful result carrying a value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The operation's output.</param>
    /// <returns>A successful <see cref="Result{T}"/>.</returns>
    public static Result<T> Success<T>(T value) => new(value, true, string.Empty);

    /// <summary>Creates a failed result for an operation that would have returned a value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="error">A user-presentable message describing what went wrong.</param>
    /// <returns>A failed <see cref="Result{T}"/>.</returns>
    public static Result<T> Failure<T>(string error) => new(default, false, error);
}
