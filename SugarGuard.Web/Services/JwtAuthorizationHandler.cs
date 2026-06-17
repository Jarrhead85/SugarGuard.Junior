using System.Net.Http.Headers;

namespace SugarGuard.Web.Services;

public class JwtAuthorizationHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;

    public JwtAuthorizationHandler(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await _tokenStore.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (InvalidOperationException)
        {

        }

        return await base.SendAsync(request, cancellationToken);
    }
}
