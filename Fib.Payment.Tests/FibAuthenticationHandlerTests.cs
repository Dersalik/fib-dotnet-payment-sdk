using Fib.Payment.Configuration;
using Fib.Payment.Exceptions;
using Fib.Payment.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace Fib.Payment.Tests;

public class FibAuthenticationHandlerTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IOptions<FibOptions>> _optionsMock;
    private readonly FibOptions _fibOptions;
    private readonly Mock<HttpMessageHandler> _innerHandlerMock;
    private readonly Mock<HttpMessageHandler> _authHandlerMock;
    private readonly List<HttpClient> _disposableClients;

    public FibAuthenticationHandlerTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _optionsMock = new Mock<IOptions<FibOptions>>();
        _innerHandlerMock = new Mock<HttpMessageHandler>();
        _authHandlerMock = new Mock<HttpMessageHandler>();
        _disposableClients = new List<HttpClient>();

        _fibOptions = new FibOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            BaseUrl = "https://test.fib.iq",
            TokenRefreshBufferSeconds = 60
        };

        _optionsMock.Setup(x => x.Value).Returns(_fibOptions);
    }

    public void Dispose()
    {
        foreach (var client in _disposableClients)
        {
            client?.Dispose();
        }
        _disposableClients.Clear();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOptionsIsNull()
    {
        // Act
        var act = () => new FibAuthenticationHandler(null!, _httpClientFactoryMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public async Task SendAsync_ShouldAuthenticateAndAddBearerToken_OnFirstRequest()
    {
        // Arrange
        var tokenResponse = new TokenResponse
        {
            AccessToken = "test-access-token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        SetupAuthenticationResponse(tokenResponse);
        SetupSuccessfulApiResponse();

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        VerifyAuthenticationCalled(Times.Once());
        request.Headers.Authorization.Should().NotBeNull();
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("test-access-token");
        VerifyInnerHandlerCalled(Times.Exactly(1));
    }

    [Fact]
    public async Task SendAsync_ShouldReuseToken_WhenTokenIsValid()
    {
        // Arrange
        var tokenResponse = new TokenResponse
        {
            AccessToken = "test-access-token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        SetupAuthenticationResponse(tokenResponse);
        SetupSuccessfulApiResponse();

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);

        // Act
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test1"));
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test2"));

        // Assert
        VerifyAuthenticationCalled(Times.Once());
        VerifyInnerHandlerCalled(Times.Exactly(2));
    }

    [Fact]
    public async Task SendAsync_ShouldRefreshToken_WhenTokenIsExpired()
    {
        // Arrange
        var firstToken = new TokenResponse
        {
            AccessToken = "first-token",
            ExpiresIn = 1, // Expires very soon
            TokenType = "Bearer"
        };

        var secondToken = new TokenResponse
        {
            AccessToken = "second-token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        SetupAuthenticationResponse(firstToken, secondToken);
        SetupSuccessfulApiResponse();

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);

        // Act
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test1"));
        await Task.Delay(1500);
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test2");
        await client.SendAsync(request2);

        // Assert
        request2.Headers.Authorization!.Parameter.Should().Be("second-token");
        VerifyAuthenticationCalled(Times.Exactly(2));
        VerifyInnerHandlerCalled(Times.Exactly(2));
    }

    [Fact]
    public async Task SendAsync_ShouldRetryWithNewToken_WhenReceivingUnauthorized()
    {
        // Arrange
        var firstToken = new TokenResponse
        {
            AccessToken = "old-token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        var secondToken = new TokenResponse
        {
            AccessToken = "new-token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        SetupAuthenticationResponse(firstToken, secondToken);
        SetupUnauthorizedThenSuccessResponse();

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);

        var requestFactory = () => new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        // Act
        using var request = requestFactory();
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        VerifyAuthenticationCalled(Times.Exactly(1));
        VerifyInnerHandlerCalled(Times.Exactly(2));
    }

    [Fact]
    public async Task SendAsync_ShouldThrowFibAuthenticationException_WhenAuthenticationFails()
    {
        // Arrange
        var errorResponse = new AuthenticationError
        {
            Error = "invalid_client",
            ErrorDescription = "Invalid client credentials"
        };

        SetupFailedAuthenticationResponse(errorResponse);

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        // Act
        var act = async () => await client.SendAsync(request);

        // Assert
        await act.Should().ThrowAsync<FibAuthenticationException>()
            .WithMessage("*Invalid client credentials*");
    }

    [Fact]
    public async Task SendAsync_ShouldHandleConcurrentRequests_WithSingleAuthentication()
    {
        // Arrange
        var tokenResponse = new TokenResponse
        {
            AccessToken = "test-access-token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        SetupAuthenticationResponse(tokenResponse);
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
        VerifyAuthenticationCalled(Times.Once());
        VerifyInnerHandlerCalled(Times.Exactly(10));
    }

    [Fact]
    public async Task SendAsync_ShouldUseTokenRefreshBuffer_WhenCheckingExpiration()
    {
        // Arrange
        _fibOptions.TokenRefreshBufferSeconds = 120;

        var tokenResponse = new TokenResponse
        {
            AccessToken = "test-token",
            ExpiresIn = 100, 
            TokenType = "Bearer"
        };

        var newTokenResponse = new TokenResponse
        {
            AccessToken = "refreshed-token",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        SetupAuthenticationResponse(tokenResponse, newTokenResponse);
        SetupSuccessfulApiResponse();

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);

        // Act
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test1");
        await client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test2");
        await client.SendAsync(request2);

        // Assert
        request2.Headers.Authorization!.Parameter.Should().Be("refreshed-token");
        VerifyAuthenticationCalled(Times.Exactly(2));
        VerifyInnerHandlerCalled(Times.Exactly(2));
    }

    [Fact]
    public void Dispose_ShouldDisposeResources()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        handler.Dispose();

        // Assert - Should not throw
        Action act = () => handler.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldIncludeCorrectCredentials_InRequest()
    {
        // Arrange
        FormUrlEncodedContent? capturedContent = null;

        _authHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                if (req.Content is FormUrlEncodedContent content)
                {
                    capturedContent = content;
                }
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new TokenResponse
                {
                    AccessToken = "token",
                    ExpiresIn = 3600,
                    TokenType = "Bearer"
                }))
            });

        _httpClientFactoryMock.Setup(x => x.CreateClient(HttpClientNames.Auth))
            .Returns(() =>
            {
                var authClient = new HttpClient(_authHandlerMock.Object)
                {
                    BaseAddress = new Uri(_fibOptions.BaseUrl)
                };
                _disposableClients.Add(authClient);
                return authClient;
            });

        SetupSuccessfulApiResponse();

        using var handler = CreateHandler();
        using var client = new HttpClient(handler);

        // Act
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test"));

        // Assert
        capturedContent.Should().NotBeNull();
        var formData = await capturedContent!.ReadAsStringAsync();
        formData.Should().Contain("grant_type=client_credentials");
        formData.Should().Contain($"client_id={_fibOptions.ClientId}");
        formData.Should().Contain($"client_secret={_fibOptions.ClientSecret}");
    }

    private FibAuthenticationHandler CreateHandler()
    {
        var handler = new FibAuthenticationHandler(_optionsMock.Object, _httpClientFactoryMock.Object)
        {
            InnerHandler = _innerHandlerMock.Object
        };
        return handler;
    }

    private void SetupAuthenticationResponse(params TokenResponse[] tokens)
    {
        var queue = new Queue<TokenResponse>(tokens);

        _authHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var token = queue.Count > 0 ? queue.Dequeue() : tokens.Last();
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(token))
                };
            });

        _httpClientFactoryMock.Setup(x => x.CreateClient(HttpClientNames.Auth))
            .Returns(() =>
            {
                var authClient = new HttpClient(_authHandlerMock.Object)
                {
                    BaseAddress = new Uri(_fibOptions.BaseUrl)
                };
                _disposableClients.Add(authClient);
                return authClient;
            });
    }

    private void SetupFailedAuthenticationResponse(AuthenticationError error)
    {
        _authHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(JsonSerializer.Serialize(error))
            });

        _httpClientFactoryMock.Setup(x => x.CreateClient(HttpClientNames.Auth))
            .Returns(() =>
            {
                var authClient = new HttpClient(_authHandlerMock.Object)
                {
                    BaseAddress = new Uri(_fibOptions.BaseUrl)
                };
                _disposableClients.Add(authClient);
                return authClient;
            });
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

    private void VerifyAuthenticationCalled(Times times)
    {
        _authHandlerMock.Protected()
            .Verify("SendAsync", times,
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("protocol/openid-connect/token")),
                ItExpr.IsAny<CancellationToken>());
    }

    private void VerifyInnerHandlerCalled(Times times)
    {
        _innerHandlerMock.Protected()
            .Verify("SendAsync", times,
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }
}