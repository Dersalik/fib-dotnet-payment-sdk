using Fib.Payment.Exceptions;
using Fib.Payment.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Fib.Payment;

/// <summary>
/// Service for managing FIB payment operations including creation, status checking, cancellation, and refunds.
/// </summary>
public class FibPaymentService
{
    private readonly HttpClient _client;

    private const string PaymentBasePath = "protected/v1/payments";

    /// <summary>
    /// Initializes a new instance of the <see cref="FibPaymentService"/> class.
    /// </summary>
    /// <param name="client">The HTTP client used for making API requests.</param>
    public FibPaymentService(HttpClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Creates a new payment asynchronously with the specified options.
    /// </summary>
    /// <param name="options">The payment creation options.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the payment creation response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="FibPaymentException">Thrown when the payment creation fails.</exception>
    public async Task<CreatePaymentResponse> CreatePaymentAsync(
        PaymentOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var req = options.ToRequest();
        var response = await _client.PostAsJsonAsync(PaymentBasePath, req, ct);

        return await HandleResponseAsync<CreatePaymentResponse>(response, ct);
    }

    /// <summary>
    /// Creates a new payment asynchronously with basic payment details.
    /// </summary>
    /// <param name="amount">The payment amount.</param>
    /// <param name="currency">The currency type.</param>
    /// <param name="callbackUrl">The callback URL for payment status updates.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the payment creation response.</returns>
    /// <exception cref="FibPaymentException">Thrown when the payment creation fails.</exception>
    public Task<CreatePaymentResponse> CreatePaymentAsync(
        decimal amount, Currency currency, string callbackUrl, CancellationToken ct = default)
    {
        return CreatePaymentAsync(new PaymentOptions
        {
            Amount = amount,
            Currency = currency,
            StatusCallbackUrl = callbackUrl
        }, ct);
    }

    /// <summary>
    /// Checks the status of a payment asynchronously.
    /// </summary>
    /// <param name="id">The unique payment identifier.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the payment status response.</returns>
    /// <exception cref="FibPaymentException">Thrown when the status check fails.</exception>
    public async Task<CheckPaymentResponse> CheckPaymentAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _client.GetAsync($"{PaymentBasePath}/{id}/status", ct);
        return await HandleResponseAsync<CheckPaymentResponse>(response, ct);
    }

    /// <summary>
    /// Cancels a payment asynchronously.
    /// </summary>
    /// <param name="id">The unique payment identifier.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a boolean indicating success.</returns>
    /// <exception cref="FibPaymentException">Thrown when the cancellation fails.</exception>
    public Task<bool> CancelPaymentAsync(Guid id, CancellationToken ct = default)
        => PostExpectStatusAsync($"{PaymentBasePath}/{id}/cancel", HttpStatusCode.NoContent, ct);

    /// <summary>
    /// Requests a refund for a payment asynchronously.
    /// </summary>
    /// <param name="id">The unique payment identifier.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a boolean indicating success.</returns>
    /// <exception cref="FibPaymentException">Thrown when the refund request fails.</exception>
    public Task<bool> RefundPaymentAsync(Guid id, CancellationToken ct = default)
        => PostExpectStatusAsync($"{PaymentBasePath}/{id}/refund", HttpStatusCode.Accepted, ct);

    private async Task<bool> PostExpectStatusAsync(
        string url, HttpStatusCode expected, CancellationToken ct)
    {
        var response = await _client.PostAsync(url, null, ct);
        if (response.StatusCode == expected)
            return true;

        await HandleResponseAsync<object>(response, ct);
        return false;
    }

    private async Task<T> HandleResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(object) || string.IsNullOrWhiteSpace(body))
                return default!;

            return JsonSerializer.Deserialize<T>(body)
                   ?? throw new FibPaymentException("Failed to deserialize response.");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new FibPaymentException("Unauthorized. Check credentials.");

        try
        {
            var error = JsonSerializer.Deserialize<PaymentErrorBody>(body);
            if (error != null)
                throw new FibPaymentException(error, (int)response.StatusCode);
        }
        catch (JsonException)
        {
            throw new FibPaymentException(
                $"Error {response.StatusCode}. Response: {body}");
        }

        throw new FibPaymentException(
            $"Payment operation failed with status {response.StatusCode}");
    }
}
