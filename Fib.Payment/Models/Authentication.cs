using System.Text.Json.Serialization;

namespace Fib.Payment.Models;

/// <summary>
/// Authentication token response
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// Gets or sets the access token for API authentication
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token lifetime in seconds
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the type of token (usually "Bearer")
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the not-before policy timestamp
    /// </summary>
    [JsonPropertyName("not-before-policy")]
    public int NotBeforePolicy { get; set; }

    /// <summary>
    /// Gets or sets the token scope
    /// </summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the calculated expiration time (UTC)
    /// </summary>
    [JsonIgnore]
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Authentication error response
/// </summary>
public class AuthenticationError
{
    /// <summary>
    /// Gets or sets the error code
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error description
    /// </summary>
    [JsonPropertyName("error_description")]
    public string ErrorDescription { get; set; } = string.Empty;
}