namespace shopify_saas_Core.Options;

public sealed class AppForgeOptions
{
    public const string SectionName = "AppForge";

    public string MobileProjectPath { get; set; } = "../appforge-mobile";

    public bool BuildEnabled { get; set; } = false;

    public string PublicBaseUrl { get; set; } = "";
}
