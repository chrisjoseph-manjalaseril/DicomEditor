namespace DicomEditor.Core;

/// <summary>
/// Generic result wrapper for operations that can succeed or fail.
/// Provides a consistent pattern for error handling across the application.
/// </summary>
/// <typeparam name="T">The type of the result value</typeparam>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private Result(bool isSuccess, T? value, string? errorMessage, Exception? exception)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Failure(string errorMessage, Exception? exception = null) 
        => new(false, default, errorMessage, exception);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(ErrorMessage!);
}

/// <summary>
/// Result type for operations that don't return a value.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private Result(bool isSuccess, string? errorMessage, Exception? exception)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string errorMessage, Exception? exception = null) 
        => new(false, errorMessage, exception);
}
