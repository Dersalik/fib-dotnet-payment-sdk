using System.Text.Json.Serialization;

namespace Fib.Payment.Models;

/// <summary>
/// Response from payment creation
/// </summary>
public class CreatePaymentResponse
{
    /// <summary>
    /// Gets or sets the unique payment identifier
    /// </summary>
    [JsonPropertyName("paymentId")]
    public Guid PaymentId { get; set; }

    /// <summary>
    /// Gets or sets the human-readable payment code
    /// </summary>
    [JsonPropertyName("readableCode")]
    public string ReadableCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the QR code for payment
    /// </summary>
    [JsonPropertyName("qrCode")]
    public string QrCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the payment expiration date and time
    /// </summary>
    [JsonPropertyName("validUntil")]
    public DateTime ValidUntil { get; set; }

    /// <summary>
    /// Gets or sets the deep link for FIB Personal App
    /// </summary>
    [JsonPropertyName("personalAppLink")]
    public string PersonalAppLink { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the deep link for FIB Business App
    /// </summary>
    [JsonPropertyName("businessAppLink")]
    public string BusinessAppLink { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the deep link for FIB Corporate App
    /// </summary>
    [JsonPropertyName("corporateAppLink")]
    public string CorporateAppLink { get; set; } = string.Empty;
}

/// <summary>
/// Paid by information
/// </summary>
public class PaidBy
{
    /// <summary>
    /// Gets or sets the name of the payer
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the IBAN of the payer
    /// </summary>
    [JsonPropertyName("iban")]
    public string Iban { get; set; } = string.Empty;
}

/// <summary>
/// Payment status check response
/// </summary>
public class CheckPaymentResponse
{
    /// <summary>
    /// Gets or sets the unique payment identifier
    /// </summary>
    [JsonPropertyName("paymentId")]
    public Guid PaymentId { get; set; }

    /// <summary>
    /// Gets or sets the current payment status
    /// </summary>
    [JsonPropertyName("status")]
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the payment was completed
    /// </summary>
    [JsonPropertyName("paidAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// Gets or sets the payment amount and currency
    /// </summary>
    [JsonPropertyName("amount")]
    public MonetaryValue Amount { get; set; } = new();

    /// <summary>
    /// Gets or sets the reason for payment decline, if applicable
    /// </summary>
    [JsonPropertyName("decliningReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaymentDecliningReason? DecliningReason { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the payment was declined
    /// </summary>
    [JsonPropertyName("declinedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? DeclinedAt { get; set; }

    /// <summary>
    /// Gets or sets information about who paid for this payment
    /// </summary>
    [JsonPropertyName("paidBy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaidBy? PaidBy { get; set; }

    /// <summary>
    /// Gets or sets the payment description
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}