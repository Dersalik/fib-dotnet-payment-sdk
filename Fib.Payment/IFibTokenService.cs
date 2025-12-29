namespace Fib.Payment;

/// <summary>
/// Service for managing FIB authentication tokens
/// </summary>
public interface IFibTokenService
{
    /// <summary>
    /// Gets a valid access token, refreshing if necessary
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A valid access token</returns>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a token refresh regardless of current token state
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A new access token</returns>
    Task<string> RefreshTokenAsync(CancellationToken cancellationToken = default);
}