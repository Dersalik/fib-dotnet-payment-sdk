using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Fib.Payment.Models;

/// <summary>
/// Payment status enumeration
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverterWithAttributeSupport<PaymentStatus>))]
public enum PaymentStatus
{
    /// <summary>
    /// Payment has been completed successfully
    /// </summary>
    [EnumMember(Value = "PAID")]
    Paid,

    /// <summary>
    /// Payment has not been completed yet
    /// </summary>
    [EnumMember(Value = "UNPAID")]
    Unpaid,

    /// <summary>
    /// Payment has been declined
    /// </summary>
    [EnumMember(Value = "DECLINED")]
    Declined,

    /// <summary>
    /// Refund has been requested for this payment
    /// </summary>
    [EnumMember(Value = "REFUND_REQUESTED")]
    RefundRequested,

    /// <summary>
    /// Payment has been refunded
    /// </summary>
    [EnumMember(Value = "REFUNDED")]
    Refunded
}

/// <summary>
/// Payment declining reason enumeration
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverterWithAttributeSupport<PaymentDecliningReason>))]
public enum PaymentDecliningReason
{
    /// <summary>
    /// Payment declined due to server failure
    /// </summary>
    [EnumMember(Value = "SERVER_FAILURE")]
    ServerFailure,

    /// <summary>
    /// Payment declined due to expiration
    /// </summary>
    [EnumMember(Value = "PAYMENT_EXPIRATION")]
    PaymentExpiration,

    /// <summary>
    /// Payment was cancelled by user or system
    /// </summary>
    [EnumMember(Value = "PAYMENT_CANCELLATION")]
    PaymentCancellation
}