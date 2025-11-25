using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Fib.Payment.Models;

/// <summary>
/// Supported currency types
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Currency
{
    /// <summary>
    /// Iraqi Dinar
    /// </summary>
    [EnumMember(Value = "IQD")]
    IQD,

    /// <summary>
    /// United States Dollar
    /// </summary>
    [EnumMember(Value = "USD")]
    USD,

    /// <summary>
    /// Euro
    /// </summary>
    [EnumMember(Value = "EUR")]
    EUR
}

/// <summary>
/// Monetary value with amount and currency
/// </summary>
public class MonetaryValue
{
    /// <summary>
    /// Gets or sets the payment amount
    /// </summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency type
    /// </summary>
    [JsonPropertyName("currency")]
    public Currency Currency { get; set; } = Currency.IQD;
}

/// <summary>
/// Payment creation request
/// </summary>
public class CreatePaymentRequest
{
    /// <summary>
    /// Gets or sets the monetary value for the payment
    /// </summary>
    [JsonPropertyName("monetaryValue")]
    public MonetaryValue MonetaryValue { get; set; } = new();

    /// <summary>
    /// Gets or sets the callback URL for payment status updates
    /// </summary>
    [JsonPropertyName("statusCallbackUrl")]
    public string? StatusCallbackUrl { get; set; } = null;

    /// <summary>
    /// Gets or sets the payment description
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the payment expiration time in ISO 8601 duration format
    /// </summary>
    [JsonPropertyName("expiresIn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpiresIn { get; set; }
}

/// <summary>
/// Payment creation options (builder pattern)
/// </summary>
public class PaymentOptions
{
    /// <summary>
    /// Gets or sets the payment amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency type
    /// </summary>
    public Currency Currency { get; set; } = Currency.IQD;

    /// <summary>
    /// Gets or sets the callback URL for payment status updates
    /// </summary>
    public string? StatusCallbackUrl { get; set; } = null;

    /// <summary>
    /// Gets or sets the payment description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the payment expiration time in ISO 8601 duration format
    /// </summary>
    public string? ExpiresIn { get; set; }

    /// <summary>
    /// Converts the payment options to a payment creation request
    /// </summary>
    /// <returns>A <see cref="CreatePaymentRequest"/> instance</returns>
    public CreatePaymentRequest ToRequest()
    {
        return new CreatePaymentRequest
        {
            MonetaryValue = new MonetaryValue
            {
                Amount = Amount,
                Currency = Currency
            },
            StatusCallbackUrl = StatusCallbackUrl,
            Description = Description,
            ExpiresIn = ExpiresIn
        };
    }
}