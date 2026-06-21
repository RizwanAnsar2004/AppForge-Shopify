using shopify_saas_Core.Constants;

namespace shopify_saas_Core.Helpers;

public static class ShopifyUrlHelper
{
    public static string ForAuthorize(string shop, string clientId, string scopes, string appUrl)
    {
        var redirectUri = $"{appUrl.TrimEnd('/')}/auth/callback";
        return $"{string.Format(ShopifyApiUrls.Authorize, shop)}" +
               $"?client_id={Uri.EscapeDataString(clientId)}" +
               $"&scope={Uri.EscapeDataString(scopes)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";
    }

    public static string ForAccessToken(string shop) => string.Format(ShopifyApiUrls.AccessToken, shop);
    public static string ForAdminGraphQl(string shop, string apiVersion) => string.Format(ShopifyApiUrls.AdminGraphQl, shop, apiVersion);
    public static string ForStorefrontGraphQl(string shop, string apiVersion) => string.Format(ShopifyApiUrls.StorefrontGraphQl, shop, apiVersion);
}
