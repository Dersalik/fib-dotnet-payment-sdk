using System.Net.Http.Headers;

namespace Fib.Payment;

/// <summary>
/// HTTP message handler that automatically handles authentication for FIB Payment Gateway requests
/// </summary>
public class FibAuthenticationHandler : DelegatingHandler
{
    private readonly IFibTokenService _tokenService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FibAuthenticationHandler"/> class
    /// </summary>
    /// <param name="tokenService">The FIB token service</param>
    /// <exception cref="ArgumentNullException">Thrown when tokenService is null</exception>
    public FibAuthenticationHandler(IFibTokenService tokenService)
    {
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
    }

    /// <summary>
    /// Sends an HTTP request with automatic authentication handling and retry logic for unauthorized responses
    /// </summary>
    /// <param name="request">The HTTP request message to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the HTTP response message</returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenService.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            token = await _tokenService.RefreshTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }
}