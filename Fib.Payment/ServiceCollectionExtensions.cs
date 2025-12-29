using Fib.Payment.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fib.Payment;

/// <summary>
/// Extension methods for IServiceCollection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add FIB Payment services to the service collection using configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration section containing FIB options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFibPayment(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FibOptions>(configuration.GetSection(FibOptions.SectionName));
        ConfigureFibServices(services);
        return services;
    }

    /// <summary>
    /// Add FIB Payment services to the service collection with explicit options configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure FIB options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFibPayment(
        this IServiceCollection services,
        Action<FibOptions> configureOptions)
    {
        services.Configure<FibOptions>(configureOptions);
        ConfigureFibServices(services);
        return services;
    }

    private static void ConfigureFibServices(IServiceCollection services)
    {
        services.AddSingleton<IFibTokenService, FibTokenService>();
        services.AddTransient<FibAuthenticationHandler>();

        services.AddHttpClient<FibPaymentService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<FibOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(25);
        })
        .AddHttpMessageHandler<FibAuthenticationHandler>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        });

        services.AddHttpClient(HttpClientNames.Auth, (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<FibOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(25);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        });
    }
}