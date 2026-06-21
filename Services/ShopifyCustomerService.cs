using System.Text.Json;
using Microsoft.Extensions.Options;
using shopify_saas_Core.Helpers;
using shopify_saas_Core.Options;

namespace shopify_saas_Core.Services;

public sealed class ShopifyCustomerService
{
    private readonly ApiCallerHelper _api;
    private readonly ShopifyOptions _options;
    private readonly ILogger<ShopifyCustomerService> _logger;

    public ShopifyCustomerService(ApiCallerHelper api, IOptions<ShopifyOptions> options, ILogger<ShopifyCustomerService> logger)
    {
        _api = api;
        _options = options.Value;
        _logger = logger;
    }

    // Creates a throwaway test customer and logs them in to obtain a customer access token.
    // Works only with CLASSIC customer accounts; new customer accounts require the
    // Customer Account API (OAuth + PKCE), which can't be done from the backend.
    public async Task<string?> CreateTestCustomerTokenAsync(string shop, string storefrontToken, CancellationToken ct = default)
    {
        var url = ShopifyUrlHelper.ForStorefrontGraphQl(shop, _options.ApiVersion);
        var headers = new Dictionary<string, string> { ["X-Shopify-Storefront-Access-Token"] = storefrontToken };

        var email = $"appforge-test+{DateTime.UtcNow:yyyyMMddHHmmss}@example.com";
        const string password = "AppForgeTest123!";

        if (!await CreateCustomerAsync(url, headers, email, password, ct))
            return null;

        return await GetCustomerTokenAsync(url, headers, email, password, ct);
    }

    private async Task<bool> CreateCustomerAsync(string url, Dictionary<string, string> headers, string email, string password, CancellationToken ct)
    {
        const string mutation = """
            mutation CustomerCreate($input: CustomerCreateInput!) {
              customerCreate(input: $input) {
                customer { id email }
                customerUserErrors { field message code }
              }
            }
            """;
        var variables = new { input = new { email, password, firstName = "AppForge", lastName = "Tester" } };

        var resp = await _api.PostJsonAsync<JsonElement>(url, new { query = mutation, variables }, headers, ct);
        if (GraphQlHelper.HasErrors(resp, _logger, "customerCreate")) return false;

        var node = resp.GetProperty("data").GetProperty("customerCreate");
        if (LogUserErrors(node, "customerCreate")) return false;

        _logger.LogInformation("Created test customer {Email}", email);
        return true;
    }

    private async Task<string?> GetCustomerTokenAsync(string url, Dictionary<string, string> headers, string email, string password, CancellationToken ct)
    {
        const string mutation = """
            mutation CustomerAccessTokenCreate($input: CustomerAccessTokenCreateInput!) {
              customerAccessTokenCreate(input: $input) {
                customerAccessToken { accessToken expiresAt }
                customerUserErrors { field message code }
              }
            }
            """;
        var variables = new { input = new { email, password } };

        var resp = await _api.PostJsonAsync<JsonElement>(url, new { query = mutation, variables }, headers, ct);
        if (GraphQlHelper.HasErrors(resp, _logger, "customerAccessTokenCreate")) return null;

        var node = resp.GetProperty("data").GetProperty("customerAccessTokenCreate");
        if (LogUserErrors(node, "customerAccessTokenCreate")) return null;

        var tokenNode = node.GetProperty("customerAccessToken");
        if (tokenNode.ValueKind == JsonValueKind.Null) return null;

        var accessToken = tokenNode.GetProperty("accessToken").GetString();
        _logger.LogInformation("Customer access token for {Email}: {Token}", email, accessToken);
        return accessToken;
    }

    private bool LogUserErrors(JsonElement node, string context)
    {
        var errors = node.GetProperty("customerUserErrors");
        if (errors.GetArrayLength() == 0) return false;

        foreach (var e in errors.EnumerateArray())
            _logger.LogWarning("{Context} userError ({Code}): {Message}",
                context,
                e.TryGetProperty("code", out var c) ? c.GetString() : "",
                e.GetProperty("message").GetString());
        return true;
    }
}
