namespace shopify_saas_Core.Options;

public sealed class ShopifyOptions
{
    public const string SectionName = "Shopify";

    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string Scopes { get; set; } = "";
    public string AppUrl { get; set; } = "";
    public string ApiVersion { get; set; } = "";
}
