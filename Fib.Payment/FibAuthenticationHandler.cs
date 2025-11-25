using Fib.Payment.Configuration;
using Fib.Payment.Exceptions;
using Fib.Payment.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Fib.Payment;

/// <summary>
/// HTTP message handler that automatically handles authentication for FIB Payment Gateway requests
/// </summary>
public class FibAuthenticationHandler : DelegatingHandler
{
    private readonly IOptions<FibOptions> _options;
    private TokenResponse? _token;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FibAuthenticationHandler"/> class
    /// </summary>
    /// <param name="options">The FIB configuration options</param>
    /// <param name="httpClientFactory">The FIB http client factory</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null</exception>
    public FibAuthenticationHandler(IOptions<FibOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Sends an HTTP request with automatic authentication handling and retry logic for unauthorized responses
    /// </summary>
    /// <param name="request">The HTTP request message to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the HTTP response message</returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token!.AccessToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await AuthenticateAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token!.AccessToken);
            response = await base.SendAsync(request, cancellationToken);
        }
        return response;
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_token == null || IsTokenExpired())
        {
            await AuthenticateAsync(cancellationToken);
        }
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_token != null && !IsTokenExpired())
            {
                return;
            }
            using var authClient = _httpClientFactory.CreateClient(HttpClientNames.Auth);
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _options.Value.ClientId),
                new KeyValuePair<string, string>("client_secret", _options.Value.ClientSecret)
            ]);
            var response = await authClient.PostAsync(
                "auth/realms/fib-online-shop/protocol/openid-connect/token",
                content,
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = JsonSerializer.Deserialize<AuthenticationError>(responseBody);
                throw new FibAuthenticationException(
                    error?.Error ?? "Unknown error",
                    error?.ErrorDescription ?? "Authentication failed"
                );
            }

            _token = JsonSerializer.Deserialize<TokenResponse>(responseBody);
            if (_token != null)
            {
                _token.ExpiresAt = DateTime.UtcNow.AddSeconds(_token.ExpiresIn);
            }
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private bool IsTokenExpired()
    {
        if (_token == null) return true;

        var bufferTime = TimeSpan.FromSeconds(_options.Value.TokenRefreshBufferSeconds);
        return DateTime.UtcNow.Add(bufferTime) >= _token.ExpiresAt;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="FibAuthenticationHandler"/> and optionally disposes of the managed resources
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tokenLock?.Dispose();
        }
        base.Dispose(disposing);
    }
}