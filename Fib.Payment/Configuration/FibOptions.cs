namespace Fib.Payment.Configuration;

/// <summary>
/// Configuration options for FIB Payment Gateway
/// </summary>
public class FibOptions
{
    /// <summary>
    /// The configuration section name for FIB options
    /// </summary>
    public const string SectionName = "Fib";

    /// <summary>
    /// Client ID provided by FIB
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client Secret provided by FIB
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for FIB API
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Token refresh buffer in seconds (refresh token before it expires)
    /// </summary>
    public int TokenRefreshBufferSeconds { get; set; } = 30;
}