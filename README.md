# Fib.NET - First Iraqi Bank Payment Gateway SDK

[![CI](https://github.com/Dersalik/fib-dotnet-payment-sdk/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Dersalik/fib-dotnet-payment-sdk/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/Fib.Payment.svg)](https://www.nuget.org/packages/Fib.Payment/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A modern, type-safe .NET SDK for integrating with the First Iraqi Bank (FIB) Online Payment Gateway. Built with .NET 8.0, this library provides a simple and intuitive API for processing payments in Iraq.

## 📦 Installation

Install the package via NuGet Package Manager:

```bash
dotnet add package Fib.Payment
```

Or via Package Manager Console:

```powershell
Install-Package Fib.Payment
```

## 🚀 Quick Start

### 1. Configure Services

Add FIB Payment services to your dependency injection container:

**Using appsettings.json:**

```json
{
  "Fib": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "BaseUrl": "https://fib.iq",
    "TokenRefreshBufferSeconds": 60
  }
}
```

```csharp
// Program.cs or Startup.cs
using Fib.Payment;

builder.Services.AddFibPayment(builder.Configuration);
```

**Using inline configuration:**

```csharp
builder.Services.AddFibPayment(options =>
{
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
    options.BaseUrl = "https://fib.iq";
    options.TokenRefreshBufferSeconds = 60;
});
```

### 2. Inject and Use the Service

```csharp
public class PaymentController : ControllerBase
{
    private readonly FibPaymentService _paymentService;

    public PaymentController(FibPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("create-payment")]
    public async Task<IActionResult> CreatePayment()
    {
        var payment = await _paymentService.CreatePaymentAsync(
            amount: 50000,
            currency: Currency.IQD,
            callbackUrl: "https://yoursite.com/payment-callback"
        );

        return Ok(new
        {
            payment.PaymentId,
            payment.ReadableCode,
            payment.QrCode,
            payment.ValidUntil,
            payment.PersonalAppLink
        });
    }
}
```

## 📖 Usage Examples

### Creating a Payment

**Basic Payment:**

```csharp
var payment = await _paymentService.CreatePaymentAsync(
    amount: 100000,
    currency: Currency.IQD,
    callbackUrl: "https://yoursite.com/callback"
);
```

**Payment with Options:**

```csharp
var options = new PaymentOptions
{
    Amount = 100000,
    Currency = Currency.IQD,
    StatusCallbackUrl = "https://yoursite.com/callback",
    Description = "Order #12345 - Premium Subscription",
    ExpiresIn = "PT1H" // ISO 8601 duration: 1 hour
};

var payment = await _paymentService.CreatePaymentAsync(options);
```

### Checking Payment Status

```csharp
var status = await _paymentService.CheckPaymentAsync(paymentId);

switch (status.Status)
{
    case PaymentStatus.Paid:
        Console.WriteLine($"Payment completed at: {status.PaidAt}");
        Console.WriteLine($"Paid by: {status.PaidBy?.Name}");
        break;
    case PaymentStatus.Unpaid:
        Console.WriteLine("Payment is still pending");
        break;
    case PaymentStatus.Declined:
        Console.WriteLine($"Payment declined: {status.DecliningReason}");
        break;
    case PaymentStatus.Refunded:
        Console.WriteLine("Payment has been refunded");
        break;
}
```

### Canceling a Payment

```csharp
try
{
    var cancelled = await _paymentService.CancelPaymentAsync(paymentId);
    if (cancelled)
    {
        Console.WriteLine("Payment cancelled successfully");
    }
}
catch (FibPaymentException ex)
{
    Console.WriteLine($"Cancellation failed: {ex.Message}");
}
```

### Refunding a Payment

```csharp
try
{
    var refunded = await _paymentService.RefundPaymentAsync(paymentId);
    if (refunded)
    {
        Console.WriteLine("Refund requested successfully");
    }
}
catch (FibPaymentException ex)
{
    Console.WriteLine($"Refund failed: {ex.Message}");
}
```

## 🔧 Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ClientId` | string | - | Client ID provided by FIB (required) |
| `ClientSecret` | string | - | Client Secret provided by FIB (required) |
| `BaseUrl` | string | - | FIB API base URL (required) |
| `TokenRefreshBufferSeconds` | int | 30 | Seconds before token expiration to refresh |

## 🌍 Supported Currencies

- **IQD** - Iraqi Dinar
- **USD** - United States Dollar
- **EUR** - Euro

## 📊 Payment Status

| Status | Description |
|--------|-------------|
| `Paid` | Payment completed successfully |
| `Unpaid` | Payment is pending |
| `Declined` | Payment was declined |
| `RefundRequested` | Refund has been requested |
| `Refunded` | Payment has been refunded |

## 🚫 Declining Reasons

| Reason | Description |
|--------|-------------|
| `ServerFailure` | Payment declined due to server error |
| `PaymentExpiration` | Payment expired before completion |
| `PaymentCancellation` | Payment was cancelled |

## 🛡️ Error Handling

The SDK throws two types of exceptions:

### FibAuthenticationException

Thrown when authentication with FIB fails:

```csharp
try
{
    var payment = await _paymentService.CreatePaymentAsync(options);
}
catch (FibAuthenticationException ex)
{
    Console.WriteLine($"Auth Error: {ex.Error}");
    Console.WriteLine($"Description: {ex.ErrorDescription}");
}
```

### FibPaymentException

Thrown when payment operations fail:

```csharp
try
{
    var payment = await _paymentService.CreatePaymentAsync(options);
}
catch (FibPaymentException ex)
{
    Console.WriteLine($"Status Code: {ex.StatusCode}");
    Console.WriteLine($"Error: {ex.Message}");
    
    if (ex.ErrorBody != null)
    {
        Console.WriteLine($"Trace ID: {ex.ErrorBody.TraceId}");
        foreach (var error in ex.ErrorBody.Errors)
        {
            Console.WriteLine($"- {error.Code}: {error.Title}");
            Console.WriteLine($"  {error.Detail}");
        }
    }
}
```

## 🧪 Testing

The project includes comprehensive unit tests using xUnit, FluentAssertions, and Moq.

**Run tests:**

```bash
dotnet test
```

**Run tests with coverage:**

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 🏗️ Project Structure

```
Fib.NET/
├── Fib.Payment/              # Main SDK library
│   ├── Configuration/        # Configuration classes
│   ├── Exceptions/          # Custom exceptions
│   ├── Models/              # Request/response models
│   ├── FibAuthenticationHandler.cs
│   ├── FibPaymentService.cs
│   └── ServiceCollectionExtensions.cs
├── Fib.Payment.Tests/       # Unit tests
└── .github/workflows/       # CI/CD workflows
```

## 📝 Requirements

- .NET 8.0 or later
- FIB merchant account with API credentials

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🔗 Links

- [GitHub Repository](https://github.com/Dersalik/fib-dotnet-payment-sdk)
- [NuGet Package](https://www.nuget.org/packages/Fib.Payment/)
- [FIB Official Website](https://www.fib.iq/)

## 👨‍💻 Author

**Dersalik**
