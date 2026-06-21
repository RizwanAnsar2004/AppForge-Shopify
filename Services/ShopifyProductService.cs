using System.Text.Json;
using Microsoft.Extensions.Options;
using shopify_saas_Core.Constants;
using shopify_saas_Core.Helpers;
using shopify_saas_Core.Options;

namespace shopify_saas_Core.Services;

public sealed class ShopifyProductService
{
    private readonly ApiCallerHelper _api;
    private readonly ShopifyOptions _options;
    private readonly ILogger<ShopifyProductService> _logger;

    public ShopifyProductService(ApiCallerHelper api, IOptions<ShopifyOptions> options, ILogger<ShopifyProductService> logger)
    {
        _api = api;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedIfEmptyAsync(string shop, string adminToken, CancellationToken ct = default)
    {
        var url = ShopifyUrlHelper.ForAdminGraphQl(shop, _options.ApiVersion);
        var headers = new Dictionary<string, string> { ["X-Shopify-Access-Token"] = adminToken };

        var hasProducts = await HasAnyProductsAsync(url, headers, ct);
        _logger.LogInformation("Store {Shop} already has products: {HasProducts}", shop, hasProducts);

        if (hasProducts)
        {
            _logger.LogInformation("Skipping seed for {Shop} — products already exist.", shop);
            return;
        }

        _logger.LogInformation("Seeding {Count} test products into {Shop}...", SeedProducts.Items.Count, shop);

        var created = 0;
        foreach (var item in SeedProducts.Items)
        {
            if (await CreateProductAsync(url, headers, item, ct)) created++;
        }

        _logger.LogInformation("Seed complete for {Shop}: {Created}/{Total} products created.",
            shop, created, SeedProducts.Items.Count);
    }

    private async Task<bool> HasAnyProductsAsync(string url, Dictionary<string, string> headers, CancellationToken ct)
    {
        const string query = "{ products(first: 1) { edges { node { id } } } }";

        var resp = await _api.PostJsonAsync<JsonElement>(url, new { query }, headers, ct);
        if (GraphQlHelper.HasErrors(resp, _logger, "products query")) return true; // on error, don't blindly seed

        var edges = resp.GetProperty("data").GetProperty("products").GetProperty("edges");
        return edges.GetArrayLength() > 0;
    }

    private async Task<bool> CreateProductAsync(string url, Dictionary<string, string> headers, SeedProduct item, CancellationToken ct)
    {
        const string mutation = """
            mutation CreateProduct($product: ProductCreateInput!, $media: [CreateMediaInput!]) {
              productCreate(product: $product, media: $media) {
                product { id title }
                userErrors { field message }
              }
            }
            """;

        var variables = new
        {
            product = new { title = item.Title, productType = item.ProductType, status = "ACTIVE" },
            media = new[] { new { originalSource = item.ImageUrl, mediaContentType = "IMAGE" } },
        };

        var resp = await _api.PostJsonAsync<JsonElement>(url, new { query = mutation, variables }, headers, ct);
        if (GraphQlHelper.HasErrors(resp, _logger, $"productCreate '{item.Title}'")) return false;

        var productCreate = resp.GetProperty("data").GetProperty("productCreate");
        var userErrors = productCreate.GetProperty("userErrors");
        if (userErrors.GetArrayLength() > 0)
        {
            foreach (var e in userErrors.EnumerateArray())
                _logger.LogWarning("productCreate '{Title}' userError: {Message}",
                    item.Title, e.GetProperty("message").GetString());
            return false;
        }

        var product = productCreate.GetProperty("product");
        _logger.LogInformation("Created product {Id} — {Title}",
            product.GetProperty("id").GetString(), product.GetProperty("title").GetString());
        return true;
    }
}
