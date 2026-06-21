using System.Text.Json;
using Microsoft.Extensions.Options;
using shopify_saas_Core.Helpers;
using shopify_saas_Core.Options;

namespace shopify_saas_Core.Services;

public sealed class ShopifyStorefrontTokenService
{
    private readonly ApiCallerHelper _api;
    private readonly ShopifyOptions _options;
    private readonly ILogger<ShopifyStorefrontTokenService> _logger;

    public ShopifyStorefrontTokenService(ApiCallerHelper api, IOptions<ShopifyOptions> options, ILogger<ShopifyStorefrontTokenService> logger)
    {
        _api = api;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> CreateAsync(string shop, string adminToken, CancellationToken ct = default)
    {
        var url = ShopifyUrlHelper.ForAdminGraphQl(shop, _options.ApiVersion);
        var headers = new Dictionary<string, string> { ["X-Shopify-Access-Token"] = adminToken };

        const string mutation = """
            mutation CreateStorefrontToken($input: StorefrontAccessTokenInput!) {
              storefrontAccessTokenCreate(input: $input) {
                storefrontAccessToken { accessToken title }
                userErrors { field message }
              }
            }
            """;
        var variables = new { input = new { title = "AppForge Storefront Token" } };

        var resp = await _api.PostJsonAsync<JsonElement>(url, new { query = mutation, variables }, headers, ct);
        if (GraphQlHelper.HasErrors(resp, _logger, "storefrontAccessTokenCreate"))
            return null;

        var node = resp.GetProperty("data").GetProperty("storefrontAccessTokenCreate");
        var userErrors = node.GetProperty("userErrors");
        if (userErrors.GetArrayLength() > 0)
        {
            foreach (var e in userErrors.EnumerateArray())
                _logger.LogWarning("storefrontAccessTokenCreate userError: {Message}",
                    e.GetProperty("message").GetString());
            return null;
        }

        var accessToken = node.GetProperty("storefrontAccessToken").GetProperty("accessToken").GetString();
        _logger.LogInformation("Storefront token for {Shop}: {Token}", shop, accessToken);
        return accessToken;
    }
}
