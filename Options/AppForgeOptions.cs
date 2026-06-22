namespace shopify_saas_Core.Options;

// Settings for turning a builder config into a branded mobile-app build.
// The build itself reuses the appforge-mobile project's own terminal scripts
// (activate.ps1 to set up the toolchain, build-android.sh to build) — the backend
// only orchestrates them.
public sealed class AppForgeOptions
{
    public const string SectionName = "AppForge";

    // Path to the appforge-mobile Flutter project (relative to the backend content root).
    public string MobileProjectPath { get; set; } = "../appforge-mobile";

    // When false, the build endpoint writes the config + assets and reports the
    // manual build command instead of invoking the build scripts.
    public bool BuildEnabled { get; set; } = false;

    // Public base URL the *device* uses to fetch uploaded images / the built APK.
    // Falls back to Shopify:AppUrl when empty.
    public string PublicBaseUrl { get; set; } = "";
}
