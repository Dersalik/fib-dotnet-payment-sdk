using Fib.Payment.Configuration;
using Fib.Payment.Exceptions;
using Fib.Payment.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Fib.Payment;

/// <summary>
/// Singleton service for managing FIB authentication tokens
/// </summary>
public class FibTokenService : IFibTokenService, IDisposable
{
    private readonly IOptions<FibOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private TokenResponse? _token;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FibTokenService"/> class
    /// </summary>
    /// <param name="options">The FIB configuration options</param>
    /// <param name="httpClientFactory">The HTTP client factory</param>
    /// <exception cref="ArgumentNullException">Thrown when options or httpClientFactory is null</exception>
    public FibTokenService(IOptions<FibOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <inheritdoc/>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_token == null || IsTokenExpired())
        {
            await AuthenticateAsync(cancellationToken);
        }

        return _token!.AccessToken;
    }

    /// <inheritdoc/>
    public async Task<string> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        await AuthenticateAsync(cancellationToken);
        return _token!.AccessToken;
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

            var authClient = _httpClientFactory.CreateClient(HttpClientNames.Auth);
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
    /// Disposes the resources used by the token service
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the token service
    /// </summary>
    /// <param name="disposing">True to release managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _tokenLock.Dispose();
            }
            _disposed = true;
        }
    }
}