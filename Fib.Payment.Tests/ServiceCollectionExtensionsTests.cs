using Fib.Payment.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fib.Payment.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFibPayment_WithConfiguration_ShouldRegisterAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            { "Fib:ClientId", "test-client-id" },
            { "Fib:ClientSecret", "test-client-secret" },
            { "Fib:BaseUrl", "https://test.fib.iq" },
            { "Fib:TokenRefreshBufferSeconds", "60" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        services.AddFibPayment(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        // Verify FibOptions configuration
        var options = serviceProvider.GetRequiredService<IOptions<FibOptions>>().Value;
        options.Should().NotBeNull();
        options.ClientId.Should().Be("test-client-id");
        options.ClientSecret.Should().Be("test-client-secret");
        options.BaseUrl.Should().Be("https://test.fib.iq");
        options.TokenRefreshBufferSeconds.Should().Be(60);

        // Verify FibAuthenticationHandler is registered
        var handler = serviceProvider.GetService<FibAuthenticationHandler>();
        handler.Should().NotBeNull();

        // Verify FibPaymentService HttpClient is registered
        var paymentService = serviceProvider.GetService<FibPaymentService>();
        paymentService.Should().NotBeNull();

        // Verify Auth HttpClient is registered
        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        httpClientFactory.Should().NotBeNull();
        var authClient = httpClientFactory!.CreateClient(HttpClientNames.Auth);
        authClient.Should().NotBeNull();
        authClient.BaseAddress.Should().NotBeNull();
        authClient.BaseAddress!.ToString().Should().Be("https://test.fib.iq/");
    }

    [Fact]
    public void AddFibPayment_WithAction_ShouldRegisterAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFibPayment(options =>
        {
            options.ClientId = "action-client-id";
            options.ClientSecret = "action-secret";
            options.BaseUrl = "https://action.fib.iq";
            options.TokenRefreshBufferSeconds = 90;
        });
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        // Verify FibOptions configuration
        var optionsValue = serviceProvider.GetRequiredService<IOptions<FibOptions>>().Value;
        optionsValue.ClientId.Should().Be("action-client-id");
        optionsValue.ClientSecret.Should().Be("action-secret");
        optionsValue.BaseUrl.Should().Be("https://action.fib.iq");
        optionsValue.TokenRefreshBufferSeconds.Should().Be(90);

        // Verify FibAuthenticationHandler is registered
        var handler = serviceProvider.GetService<FibAuthenticationHandler>();
        handler.Should().NotBeNull();

        // Verify FibPaymentService HttpClient is registered
        var paymentService = serviceProvider.GetService<FibPaymentService>();
        paymentService.Should().NotBeNull();

        // Verify Auth HttpClient is registered
        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        httpClientFactory.Should().NotBeNull();
        var authClient = httpClientFactory!.CreateClient(HttpClientNames.Auth);
        authClient.Should().NotBeNull();
        authClient.BaseAddress.Should().NotBeNull();
        authClient.BaseAddress!.ToString().Should().Be("https://action.fib.iq/");
    }
}