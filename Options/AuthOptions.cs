namespace shopify_saas_Core.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int OtpExpiryMinutes { get; set; } = 10;
}
