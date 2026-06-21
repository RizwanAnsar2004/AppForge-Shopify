using Microsoft.Extensions.Options;
using shopify_saas_Core.Helpers;
using shopify_saas_Core.Options;

namespace shopify_saas_Core.Services;

public sealed record AdminTokenResult(string AccessToken, string Scope);

public sealed class ShopifyOAuthService
{
    private readonly ApiCallerHelper _api;
    private readonly ShopifyOptions _options;

    public ShopifyOAuthService(ApiCallerHelper api, IOptions<ShopifyOptions> options)
    {
        _api = api;
        _options = options.Value;
    }

    public string BuildAuthorizeUrl(string shop) =>
        ShopifyUrlHelper.ForAuthorize(shop, _options.ApiKey, _options.Scopes, _options.AppUrl);

    public async Task<AdminTokenResult> ExchangeCodeAsync(string shop, string code, CancellationToken ct = default)
    {
        var body = new
        {
            client_id = _options.ApiKey,
            client_secret = _options.ApiSecret,
            code,
        };

        var token = await _api.PostJsonAsync<TokenResponse>(
            ShopifyUrlHelper.ForAccessToken(shop), body, ct: ct);

        return new AdminTokenResult(token.access_token, token.scope);
    }

    private sealed record TokenResponse(string access_token, string scope);
}
