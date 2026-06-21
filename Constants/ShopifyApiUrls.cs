namespace shopify_saas_Core.Constants;

// {0} = shop domain, {1} = API version.
public static class ShopifyApiUrls
{
    // OAuth
    public const string Authorize = "https://{0}/admin/oauth/authorize";
    public const string AccessToken = "https://{0}/admin/oauth/access_token";

    // GraphQL endpoints (used as later phases land)
    public const string AdminGraphQl = "https://{0}/admin/api/{1}/graphql.json";
    public const string StorefrontGraphQl = "https://{0}/api/{1}/graphql.json";
}
