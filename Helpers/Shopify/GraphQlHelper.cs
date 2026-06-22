using System.Text.Json;

namespace shopify_saas_Core.Helpers.Shopify;

public static class GraphQlHelper
{
    public static bool HasErrors(JsonElement resp, ILogger logger, string context)
    {
        if (resp.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0)
        {
            foreach (var e in errors.EnumerateArray())
                logger.LogError("GraphQL error during {Context}: {Message}",
                    context, e.GetProperty("message").GetString());
            return true;
        }
        return false;
    }
}
