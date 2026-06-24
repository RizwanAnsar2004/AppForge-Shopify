using shopify_saas_Core.Models.RequestModels;

namespace shopify_saas_Core.Helpers.AppBuilder;

public static class ImageStore
{
    public sealed record SavedImage(string ConfigKey, string Url, int Bytes);

    public static IReadOnlyList<SavedImage> Save(string baseDir, string store, BuildImages? images, string publicBaseUrl)
    {
        if (images is null) return Array.Empty<SavedImage>();

        var dir = Path.Combine(baseDir, store);
        Directory.CreateDirectory(dir);

        var saved = new List<SavedImage>();
        Add(saved, dir, store, publicBaseUrl, images.AppIcon, "icon", "APP_ICON_URL");
        Add(saved, dir, store, publicBaseUrl, images.SplashImage, "splash", "SPLASH_IMAGE_URL");
        Add(saved, dir, store, publicBaseUrl, images.InAppLogo, "logo", "LOGO_URL");
        return saved;
    }

    private static void Add(List<SavedImage> saved, string dir, string store, string publicBaseUrl,
        ImagePayload? img, string fileBase, string configKey)
    {
        if (img is null || string.IsNullOrWhiteSpace(img.DataUrl)) return;
        var (bytes, ext) = DecodeDataUrl(img.DataUrl);
        if (bytes.Length == 0) return;

        var fileName = $"{fileBase}{ext}";
        File.WriteAllBytes(Path.Combine(dir, fileName), bytes);
        saved.Add(new SavedImage(configKey, $"{publicBaseUrl}/uploads/{store}/{fileName}", bytes.Length));
    }

    private static (byte[] bytes, string ext) DecodeDataUrl(string dataUrl)
    {
        var comma = dataUrl.IndexOf(',');
        if (comma < 0) return (Array.Empty<byte>(), ".png");
        var header = dataUrl[..comma];
        var ext = header.Contains("image/jpeg") ? ".jpg"
            : header.Contains("image/svg") ? ".svg"
            : ".png";
        try { return (Convert.FromBase64String(dataUrl[(comma + 1)..]), ext); }
        catch { return (Array.Empty<byte>(), ext); }
    }
}
