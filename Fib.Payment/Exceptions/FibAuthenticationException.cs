namespace Fib.Payment.Exceptions;

/// <summary>
/// Exception thrown when authentication fails
/// </summary>
public class FibAuthenticationException : Exception
{
    /// <summary>
    /// Gets the error code
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// Gets the error description
    /// </summary>
    public string ErrorDescription { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FibAuthenticationException"/> class
    /// </summary>
    public FibAuthenticationException()
    {
        Error = string.Empty;
        ErrorDescription = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FibAuthenticationException"/> class with error details
    /// </summary>
    /// <param name="error">The error code</param>
    /// <param name="errorDescription">The error description</param>
    public FibAuthenticationException(string error, string errorDescription)
        : base($"Authentication failed: {error} - {errorDescription}")
    {
        Error = error;
        ErrorDescription = errorDescription;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FibAuthenticationException"/> class with a custom message
    /// </summary>
    /// <param name="message">The error message</param>
    public FibAuthenticationException(string message) : base(message)
    {
        Error = string.Empty;
        ErrorDescription = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FibAuthenticationException"/> class with a custom message and inner exception
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    public FibAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = string.Empty;
        ErrorDescription = string.Empty;
    }
}