using Fib.Payment.Exceptions;
using Fib.Payment.Models;
using FluentAssertions;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Fib.Payment.Tests;

public class FibPaymentServiceTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly FibPaymentService _service;

    public FibPaymentServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://test.fib.iq/")
        };
        _service = new FibPaymentService(_httpClient);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    #region CreatePaymentAsync with PaymentOptions

    [Fact]
    public async Task CreatePaymentAsync_WithOptions_ShouldReturnCreatePaymentResponse_WhenSuccessful()
    {
        // Arrange
        var options = new PaymentOptions
        {
            Amount = 1000,
            Currency = Currency.IQD,
            StatusCallbackUrl = "https://callback.test.com",
            Description = "Test payment"
        };

        var expectedResponse = new CreatePaymentResponse
        {
            PaymentId = Guid.NewGuid(),
            ReadableCode = "12345678",
            QrCode = "qr-code-data",
            ValidUntil = DateTime.UtcNow.AddHours(1),
            PersonalAppLink = "fib://personal",
            BusinessAppLink = "fib://business",
            CorporateAppLink = "fib://corporate"
        };

        SetupSuccessResponse(expectedResponse);

        // Act
        var result = await _service.CreatePaymentAsync(options);

        // Assert
        result.Should().NotBeNull();
        result.PaymentId.Should().Be(expectedResponse.PaymentId);
        result.ReadableCode.Should().Be(expectedResponse.ReadableCode);
        result.QrCode.Should().Be(expectedResponse.QrCode);
        VerifyHttpCall(HttpMethod.Post, "protected/v1/payments", Times.Once());
    }

    [Fact]
    public async Task CreatePaymentAsync_WithOptions_ShouldThrowArgumentNullException_WhenOptionsIsNull()
    {
        // Act
        var act = async () => await _service.CreatePaymentAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public async Task CreatePaymentAsync_WithOptions_ShouldThrowFibPaymentException_WhenUnauthorized()
    {
        // Arrange
        var options = new PaymentOptions
        {
            Amount = 1000,
            Currency = Currency.IQD,
            StatusCallbackUrl = "https://callback.test.com"
        };

        SetupUnauthorizedResponse();

        // Act
        var act = async () => await _service.CreatePaymentAsync(options);

        // Assert
        await act.Should().ThrowAsync<FibPaymentException>()
            .WithMessage("Unauthorized. Check credentials.");
    }

    [Fact]
    public async Task CreatePaymentAsync_WithOptions_ShouldThrowFibPaymentException_WhenErrorResponse()
    {
        // Arrange
        var options = new PaymentOptions
        {
            Amount = 1000,
            Currency = Currency.IQD,
            StatusCallbackUrl = "https://callback.test.com"
        };

        var errorBody = new PaymentErrorBody
        {
            TraceId = "trace-123",
            Errors = new List<PaymentError>
            {
                new() { Code = "INVALID_AMOUNT", Title = "Invalid Amount", Detail = "Amount must be positive" }
            }
        };

        SetupErrorResponse(HttpStatusCode.BadRequest, errorBody);

        // Act
        var act = async () => await _service.CreatePaymentAsync(options);

        // Assert
        await act.Should().ThrowAsync<FibPaymentException>()
            .Where(ex => ex.StatusCode == 400 && ex.Message.Contains("Invalid Amount"));
    }

    #endregion

    #region CreatePaymentAsync with basic parameters

    [Fact]
    public async Task CreatePaymentAsync_WithBasicParams_ShouldReturnCreatePaymentResponse_WhenSuccessful()
    {
        // Arrange
        var expectedResponse = new CreatePaymentResponse
        {
            PaymentId = Guid.NewGuid(),
            ReadableCode = "87654321",
            QrCode = "qr-data",
            ValidUntil = DateTime.UtcNow.AddHours(2),
            PersonalAppLink = "fib://personal",
            BusinessAppLink = "fib://business",
            CorporateAppLink = "fib://corporate"
        };

        SetupSuccessResponse(expectedResponse);

        // Act
        var result = await _service.CreatePaymentAsync(5000, Currency.USD, "https://callback.test.com");

        // Assert
        result.Should().NotBeNull();
        result.PaymentId.Should().Be(expectedResponse.PaymentId);
        VerifyHttpCall(HttpMethod.Post, "protected/v1/payments", Times.Once());
    }

    #endregion

    #region CheckPaymentAsync

    [Fact]
    public async Task CheckPaymentAsync_ShouldReturnCheckPaymentResponse_WhenSuccessful()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var expectedResponse = new CheckPaymentResponse
        {
            PaymentId = paymentId,
            Status = PaymentStatus.Paid,
            PaidAt = DateTime.UtcNow,
            Amount = new MonetaryValue { Amount = 1000, Currency = Currency.IQD },
            Description = "Test payment"
        };

        SetupSuccessResponse(expectedResponse);

        // Act
        var result = await _service.CheckPaymentAsync(paymentId);

        // Assert
        result.Should().NotBeNull();
        result.PaymentId.Should().Be(paymentId);
        result.Status.Should().Be(PaymentStatus.Paid);
        result.Amount.Should().NotBeNull();
        result.Amount.Amount.Should().Be(1000);
        VerifyHttpCall(HttpMethod.Get, $"protected/v1/payments/{paymentId}/status", Times.Once());
    }

    [Fact]
    public async Task CheckPaymentAsync_ShouldReturnUnpaidStatus_WhenPaymentNotCompleted()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var expectedResponse = new CheckPaymentResponse
        {
            PaymentId = paymentId,
            Status = PaymentStatus.Unpaid,
            Amount = new MonetaryValue { Amount = 1000, Currency = Currency.IQD }
        };

        SetupSuccessResponse(expectedResponse);

        // Act
        var result = await _service.CheckPaymentAsync(paymentId);

        // Assert
        result.Status.Should().Be(PaymentStatus.Unpaid);
        result.PaidAt.Should().BeNull();
    }

    [Fact]
    public async Task CheckPaymentAsync_ShouldReturnDeclinedStatus_WithReason()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var expectedResponse = new CheckPaymentResponse
        {
            PaymentId = paymentId,
            Status = PaymentStatus.Declined,
            Amount = new MonetaryValue { Amount = 1000, Currency = Currency.IQD },
            DecliningReason = PaymentDecliningReason.PaymentExpiration,
            DeclinedAt = DateTime.UtcNow
        };

        SetupSuccessResponse(expectedResponse);

        // Act
        var result = await _service.CheckPaymentAsync(paymentId);

        // Assert
        result.Status.Should().Be(PaymentStatus.Declined);
        result.DecliningReason.Should().Be(PaymentDecliningReason.PaymentExpiration);
        result.DeclinedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckPaymentAsync_ShouldThrowFibPaymentException_WhenPaymentNotFound()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var errorBody = new PaymentErrorBody
        {
            TraceId = "trace-456",
            Errors = new List<PaymentError>
            {
                new() { Code = "NOT_FOUND", Title = "Payment Not Found", Detail = "The requested payment does not exist" }
            }
        };

        SetupErrorResponse(HttpStatusCode.NotFound, errorBody);

        // Act
        var act = async () => await _service.CheckPaymentAsync(paymentId);

        // Assert
        await act.Should().ThrowAsync<FibPaymentException>()
            .Where(ex => ex.StatusCode == 404);
    }

    #endregion

    #region CancelPaymentAsync

    [Fact]
    public async Task CancelPaymentAsync_ShouldReturnTrue_WhenSuccessful()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        SetupNoContentResponse();

        // Act
        var result = await _service.CancelPaymentAsync(paymentId);

        // Assert
        result.Should().BeTrue();
        VerifyHttpCall(HttpMethod.Post, $"protected/v1/payments/{paymentId}/cancel", Times.Once());
    }

    [Fact]
    public async Task CancelPaymentAsync_ShouldThrowFibPaymentException_WhenFails()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var errorBody = new PaymentErrorBody
        {
            TraceId = "trace-789",
            Errors = new List<PaymentError>
            {
                new() { Code = "CANNOT_CANCEL", Title = "Cannot Cancel", Detail = "Payment cannot be cancelled" }
            }
        };

        SetupErrorResponse(HttpStatusCode.BadRequest, errorBody);

        // Act
        var act = async () => await _service.CancelPaymentAsync(paymentId);

        // Assert
        await act.Should().ThrowAsync<FibPaymentException>()
            .Where(ex => ex.Message.Contains("Cannot Cancel"));
    }

    [Fact]
    public async Task CancelPaymentAsync_ShouldHandleCancellationToken()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        // Act
        var act = async () => await _service.CancelPaymentAsync(paymentId, cts.Token);

        // Assert
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    #endregion

    #region RefundPaymentAsync

    [Fact]
    public async Task RefundPaymentAsync_ShouldReturnTrue_WhenSuccessful()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        SetupAcceptedResponse();

        // Act
        var result = await _service.RefundPaymentAsync(paymentId);

        // Assert
        result.Should().BeTrue();
        VerifyHttpCall(HttpMethod.Post, $"protected/v1/payments/{paymentId}/refund", Times.Once());
    }

    [Fact]
    public async Task RefundPaymentAsync_ShouldThrowFibPaymentException_WhenPaymentNotRefundable()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var errorBody = new PaymentErrorBody
        {
            TraceId = "trace-999",
            Errors = new List<PaymentError>
            {
                new() { Code = "NOT_REFUNDABLE", Title = "Not Refundable", Detail = "Payment is not refundable" }
            }
        };

        SetupErrorResponse(HttpStatusCode.BadRequest, errorBody);

        // Act
        var act = async () => await _service.RefundPaymentAsync(paymentId);

        // Assert
        await act.Should().ThrowAsync<FibPaymentException>()
            .Where(ex => ex.Message.Contains("Not Refundable"));
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task CreatePaymentAsync_ShouldThrowFibPaymentException_WhenDeserializationFails()
    {
        // Arrange
        var options = new PaymentOptions
        {
            Amount = 1000,
            Currency = Currency.IQD,
            StatusCallbackUrl = "https://callback.test.com"
        };

        SetupInvalidJsonResponse();

        // Act
        var act = async () => await _service.CreatePaymentAsync(options);

        // Assert
        await act.Should().ThrowAsync<FibPaymentException>()
            .WithMessage("Failed to deserialize response.");
    }

    [Fact]
    public async Task CreatePaymentAsync_ShouldThrowFibPaymentException_WhenNonJsonErrorResponse()
    {
        // Arrange
        var options = new PaymentOptions
        {
            Amount = 1000,
            Currency = Currency.IQD,
            StatusCallbackUrl = "https://callback.test.com"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Internal Server Error")
            });

        // Act
        var act = async () => await _service.CreatePaymentAsync(options);

        // Assert
        await act.Should().ThrowAsync<FibPaymentException>()
            .Where(ex => ex.Message.Contains("InternalServerError"));
    }

    [Fact]
    public async Task CreatePaymentAsync_ShouldThrowFibPaymentException_WithEmptyErrorsList()
    {
        // Arrange
        var options = new PaymentOptions
        {
            Amount = 1000,
            Currency = Currency.IQD,
            StatusCallbackUrl = "https://callback.test.com"
        };

        var errorBody = new PaymentErrorBody
        {
            TraceId = "trace-empty",
            Errors = new List<PaymentError>()
        };

        SetupErrorResponse(HttpStatusCode.BadRequest, errorBody);

        // Act
        var act = async () => await _service.CreatePaymentAsync(options);

        // Assert
        await act.Should().ThrowAsync<FibPaymentException>()
            .Where(ex => ex.Message.Contains("trace-empty"));
    }

    #endregion

    #region Helper Methods

    private void SetupSuccessResponse<T>(T response)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(response))
            });
    }

    private void SetupNoContentResponse()
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NoContent
            });
    }

    private void SetupAcceptedResponse()
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Accepted
            });
    }

    private void SetupUnauthorizedResponse()
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized
            });
    }

    private void SetupErrorResponse(HttpStatusCode statusCode, PaymentErrorBody errorBody)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonSerializer.Serialize(errorBody))
            });
    }

    private void SetupInvalidJsonResponse()
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("null")
            });
    }

    private void VerifyHttpCall(HttpMethod method, string path, Times times)
    {
        _httpMessageHandlerMock.Protected()
            .Verify("SendAsync", times,
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == method &&
                    req.RequestUri!.ToString().Contains(path)),
                ItExpr.IsAny<CancellationToken>());
    }

    #endregion
}