namespace shopify_saas_Core.Models.RequestModels;

public sealed class BuildRequest
{
    public string Store { get; set; } = "";
    public Dictionary<string, string> Config { get; set; } = new();
    public BuildImages? Images { get; set; }
}

public sealed class BuildImages
{
    public ImagePayload? AppIcon { get; set; }
    public ImagePayload? SplashImage { get; set; }
    public ImagePayload? InAppLogo { get; set; }
}

public sealed class ImagePayload
{
    public string Name { get; set; } = "";
    public string DataUrl { get; set; } = "";
}
