using Microsoft.AspNetCore.Mvc;
using shopify_saas_Core.Constants;
using shopify_saas_Core.Services;

namespace shopify_saas_Core.Controllers;

// HMAC verification intentionally omitted for local dev — add back before production.
[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ShopifyOAuthService _oauth;
    private readonly ShopifyProductService _products;
    private readonly ShopifyStorefrontTokenService _storefront;
    private readonly ShopifyCustomerService _customers;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ShopifyOAuthService oauth,
        ShopifyProductService products,
        ShopifyStorefrontTokenService storefront,
        ShopifyCustomerService customers,
        ILogger<AuthController> logger)
    {
        _oauth = oauth;
        _products = products;
        _storefront = storefront;
        _customers = customers;
        _logger = logger;
    }

    // STEP 1 — start the install: redirect the merchant to Shopify to approve scopes.
    [HttpGet("install")]
    public RedirectResult Install([FromQuery] string shop)
    {
        if (!IsValidShop(shop))
            throw new Exception("Invalid 'shop'. Expected <name>.myshopify.com.");

        var url = _oauth.BuildAuthorizeUrl(shop);
        _logger.LogInformation("Starting OAuth for {Shop}", shop);
        return Redirect(url);
    }

    // STEP 2 — Shopify redirects back with ?code & ?shop. Exchange the code for an Admin token.
    [HttpGet("callback")]
    public async Task<ContentResult> Callback([FromQuery] string shop, [FromQuery] string code, CancellationToken ct)
    {
        if (!IsValidShop(shop))
            throw new Exception("Invalid 'shop'.");
        if (string.IsNullOrWhiteSpace(code))
            throw new Exception("Missing 'code'.");

        var token = await _oauth.ExchangeCodeAsync(shop, code, ct);

        _logger.LogInformation("Install complete for {Shop}. Admin token: {Token} (scopes: {Scope})",
            shop, token.AccessToken, token.Scope);

        // Seed test products if the store is empty (needs write_products scope).
        await _products.SeedIfEmptyAsync(shop, token.AccessToken, ct);

        // Mint the Storefront token, then use it to obtain a test customer access token.
        var storefrontToken = await _storefront.CreateAsync(shop, token.AccessToken, ct);
        var customerToken = storefrontToken is null
            ? null
            : await _customers.CreateTestCustomerTokenAsync(shop, storefrontToken, ct);

        const string unavailable = "(not available — check server logs)";
        var html = HtmlTemplates.InstallSuccess
            .Replace("{shop}", shop)
            .Replace("{token}", token.AccessToken)
            .Replace("{storefrontToken}", storefrontToken ?? unavailable)
            .Replace("{customerToken}", customerToken ?? unavailable);

        return Content(html, "text/html");
    }

    private static bool IsValidShop(string? shop) =>
        !string.IsNullOrWhiteSpace(shop) && shop.EndsWith(".myshopify.com", StringComparison.OrdinalIgnoreCase);
}
