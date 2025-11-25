using System.Text.Json.Serialization;

namespace Fib.Payment.Exceptions;

/// <summary>
/// Payment error detail
/// </summary>
public class PaymentError
{
    /// <summary>
    /// Gets or sets the error code
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error title
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed error description
    /// </summary>
    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;
}

/// <summary>
/// Payment error response body
/// </summary>
public class PaymentErrorBody
{
    /// <summary>
    /// Gets or sets the trace identifier for debugging
    /// </summary>
    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of errors
    /// </summary>
    [JsonPropertyName("errors")]
    public List<PaymentError> Errors { get; set; } = new();
}

/// <summary>
/// Exception thrown when payment operations fail
/// </summary>
public class FibPaymentException : Exception
{
    /// <summary>
    /// Gets the error body containing detailed error information
    /// </summary>
    public PaymentErrorBody? ErrorBody { get; }

    /// <summary>
    /// Gets the HTTP status code of the failed request
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FibPaymentException"/> class
    /// </summary>
    public FibPaymentException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FibPaymentException"/> class with error details
    /// </summary>
    /// <param name="errorBody">The error response body</param>
    /// <param name="statusCode">The HTTP status code</param>
    public FibPaymentException(PaymentErrorBody errorBody, int statusCode)
        : base(BuildMessage(errorBody))
    {
        ErrorBody = errorBody;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FibPaymentException"/> class with a custom message
    /// </summary>
    /// <param name="message">The error message</param>
    public FibPaymentException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FibPaymentException"/> class with a custom message and inner exception
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    public FibPaymentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private static string BuildMessage(PaymentErrorBody errorBody)
    {
        if (errorBody.Errors.Count == 0)
        {
            return $"Payment operation failed. TraceId: {errorBody.TraceId}";
        }

        var firstError = errorBody.Errors[0];
        return $"Payment operation failed: {firstError.Code} - {firstError.Title}. {firstError.Detail}. TraceId: {errorBody.TraceId}";
    }
}