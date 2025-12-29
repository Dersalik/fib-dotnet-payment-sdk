using FluentAssertions;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace Fib.Payment.Tests;

public class FibAuthenticationHandlerTests
{
    private readonly Mock<IFibTokenService> _tokenServiceMock;
    private readonly Mock<HttpMessageHandler> _innerHandlerMock;

    public FibAuthenticationHandlerTests()
    {
        _tokenServiceMock = new Mock<IFibTokenService>();
        _innerHandlerMock = new Mock<HttpMessageHandler>();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenTokenServiceIsNull()
    {
        // Act
        var act = () => new FibAuthenticationHandler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tokenService");
    }

    [Fact]
    public async Task SendAsync_ShouldAddBearerToken_OnRequest()
    {
        // Arrange
        _tokenServiceMock.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-access-token");

        SetupSuccessfulApiResponse();

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        request.Headers.Authorization.Should().NotBeNull();
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("test-access-token");
        _tokenServiceMock.Verify(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
        VerifyInnerHandlerCalled(Times.Once());
    }

    [Fact]
    public async Task SendAsync_ShouldRefreshTokenAndRetry_WhenReceivingUnauthorized()
    {
        // Arrange
        _tokenServiceMock.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("old-token");

        _tokenServiceMock.Setup(x => x.RefreshTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-token");

        SetupUnauthorizedThenSuccessResponse();

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tokenServiceMock.Verify(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
        _tokenServiceMock.Verify(x => x.RefreshTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
        VerifyInnerHandlerCalled(Times.Exactly(2));
    }

    [Fact]
    public async Task SendAsync_ShouldNotRefreshToken_WhenResponseIsSuccessful()
    {
        // Arrange
        _tokenServiceMock.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token");

        SetupSuccessfulApiResponse();

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);

        // Act
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test1"));
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test2"));

        // Assert
        _tokenServiceMock.Verify(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _tokenServiceMock.Verify(x => x.RefreshTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
        VerifyInnerHandlerCalled(Times.Exactly(2));
    }

    [Fact]
    public async Task SendAsync_ShouldHandleConcurrentRequests()
    {
        // Arrange
        _tokenServiceMock.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-access-token");

        SetupSuccessfulApiResponse();

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);

        // Act - Make multiple concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test")))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        _tokenServiceMock.Verify(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()), Times.Exactly(10));
        VerifyInnerHandlerCalled(Times.Exactly(10));
    }

    [Fact]
    public async Task SendAsync_ShouldPropagateException_WhenTokenServiceThrows()
    {
        // Arrange
        _tokenServiceMock.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Token service failed"));

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        // Act
        var act = async () => await client.SendAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Token service failed");
    }

    [Fact]
    public async Task SendAsync_ShouldUseNewToken_AfterRefresh()
    {
        // Arrange
        _tokenServiceMock.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("initial-token");

        _tokenServiceMock.Setup(x => x.RefreshTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("refreshed-token");

        HttpRequestMessage? capturedRetryRequest = null;
        var callCount = 0;

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized };
                }
                capturedRetryRequest = req;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"success\": true}")
                };
            });

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        // Act
        await client.SendAsync(request);

        // Assert
        capturedRetryRequest.Should().NotBeNull();
        capturedRetryRequest!.Headers.Authorization!.Parameter.Should().Be("refreshed-token");
    }

    private FibAuthenticationHandler CreateHandler()
    {
        var handler = new FibAuthenticationHandler(_tokenServiceMock.Object)
        {
            InnerHandler = _innerHandlerMock.Object
        };
        return handler;
    }

    private void SetupSuccessfulApiResponse()
    {
        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\": true}")
            });
    }

    private void SetupUnauthorizedThenSuccessResponse()
    {
        var callCount = 0;
        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized }
                    : new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("{\"success\": true}")
                    };
            });
    }

    private void VerifyInnerHandlerCalled(Times times)
    {
        _innerHandlerMock.Protected()
            .Verify("SendAsync", times,
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }
}