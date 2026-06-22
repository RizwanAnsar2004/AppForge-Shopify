using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using shopify_saas_Core.Options;

namespace shopify_saas_Core.Services;

// ---- Request DTOs (posted by the React builder) ----

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
    public string DataUrl { get; set; } = ""; // data:image/png;base64,....
}

// ---- Live build events streamed over SSE ----

public sealed record BuildEvent(string Type, string? Line = null, string? Status = null,
    string? ApkUrl = null, string? Message = null);

// A single in-flight (or finished) build. Events are buffered so a client that
// connects to the SSE stream slightly late still receives the full log.
public sealed class BuildJob
{
    public string Id { get; } = Guid.NewGuid().ToString("n");
    public string Status { get; set; } = "queued"; // queued | running | succeeded | failed
    public string? ApkUrl { get; set; }
    public volatile bool Completed;

    private readonly List<BuildEvent> _events = new();
    private readonly object _lock = new();

    public void Add(BuildEvent e)
    {
        lock (_lock) _events.Add(e);
    }

    public BuildEvent[] Since(int index)
    {
        lock (_lock) return index >= _events.Count ? Array.Empty<BuildEvent>() : _events.GetRange(index, _events.Count - index).ToArray();
    }
}

// Owns the build pipeline: writes the per-store config + assets, then (optionally)
// runs the Dockerized Flutter build, streaming stdout/stderr into the job.
public sealed class AppBuildService
{
    private readonly AppForgeOptions _opts;
    private readonly string _publicBaseUrl;
    private readonly ILogger<AppBuildService> _logger;
    private readonly ConcurrentDictionary<string, BuildJob> _jobs = new();

    public AppBuildService(
        IOptions<AppForgeOptions> opts,
        IOptions<ShopifyOptions> shopify,
        ILogger<AppBuildService> logger)
    {
        _opts = opts.Value;
        _logger = logger;
        _publicBaseUrl =
            (string.IsNullOrWhiteSpace(_opts.PublicBaseUrl) ? shopify.Value.AppUrl : _opts.PublicBaseUrl)
            .TrimEnd('/');
    }

    // Public folder (served at /downloads) where finished APKs are copied.
    public static string DownloadsDir =>
        Path.Combine(Directory.GetCurrentDirectory(), "Downloads");

    public BuildJob? Get(string id) => _jobs.TryGetValue(id, out var job) ? job : null;

    // Validate + sanitize, then kick off the build on a background task.
    public BuildJob Start(BuildRequest request)
    {
        var store = Sanitize(request.Store);
        if (store.Length == 0) throw new ArgumentException("Invalid store name.");

        var job = new BuildJob();
        _jobs[job.Id] = job;
        _ = Task.Run(() => RunAsync(job, store, request));
        return job;
    }

    private async Task RunAsync(BuildJob job, string store, BuildRequest request)
    {
        try
        {
            job.Status = "running";
            job.Add(new BuildEvent("log", Line: $"▶ Build started for store '{store}'"));

            var mobileRoot = Path.GetFullPath(_opts.MobileProjectPath, Directory.GetCurrentDirectory());
            if (!Directory.Exists(mobileRoot))
                throw new DirectoryNotFoundException($"Mobile project not found at {mobileRoot}");

            // 1) Save uploaded images and rewrite the config to point at hosted URLs.
            var config = new Dictionary<string, string>(request.Config);
            SaveImages(store, request.Images, config, job);

            // 2) Write config/<store>.json for --dart-define-from-file.
            var configDir = Path.Combine(mobileRoot, "config");
            Directory.CreateDirectory(configDir);
            var configPath = Path.Combine(configDir, $"{store}.json");
            await File.WriteAllTextAsync(configPath,
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            job.Add(new BuildEvent("log", Line: $"✓ Wrote {Path.GetRelativePath(mobileRoot, configPath)}"));

            // 3) Run (or describe) the local Flutter build.
            if (!_opts.BuildEnabled)
            {
                var cmd = OperatingSystem.IsWindows()
                    ? $".\\build-android.ps1 -Store {store}"
                    : $"bash build-android.sh {store}";
                job.Add(new BuildEvent("log", Line: "ℹ Build pipeline disabled (AppForge:BuildEnabled=false)."));
                job.Add(new BuildEvent("log", Line: "  Build it from the appforge-mobile project:"));
                job.Add(new BuildEvent("log", Line: $"  {cmd}"));
                Finish(job, "succeeded", null, "Config generated. Run the build command shown in the log.");
                return;
            }

            await RunBuildAsync(mobileRoot, store, job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build failed for {Store}", store);
            job.Add(new BuildEvent("log", Line: $"✖ {ex.Message}"));
            Finish(job, "failed", null, ex.Message);
        }
    }

    private void SaveImages(string store, BuildImages? images, IDictionary<string, string> config, BuildJob job)
    {
        if (images is null) return;

        // Images live outside wwwroot (which the SPA build wipes) and are served at /uploads.
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", store);
        Directory.CreateDirectory(dir);

        void Save(ImagePayload? img, string fileBase, string configKey)
        {
            if (img is null || string.IsNullOrWhiteSpace(img.DataUrl)) return;
            var (bytes, ext) = DecodeDataUrl(img.DataUrl);
            if (bytes.Length == 0) return;
            var fileName = $"{fileBase}{ext}";
            File.WriteAllBytes(Path.Combine(dir, fileName), bytes);
            config[configKey] = $"{_publicBaseUrl}/uploads/{store}/{fileName}";
            job.Add(new BuildEvent("log", Line: $"✓ Saved {configKey} ({bytes.Length} bytes)"));
        }

        Save(images.AppIcon, "icon", "APP_ICON_URL");
        Save(images.SplashImage, "splash", "SPLASH_IMAGE_URL");
        Save(images.InAppLogo, "logo", "LOGO_URL");
    }

    // Delegate the actual build to the mobile project's own build script — PowerShell
    // on Windows, bash elsewhere. The backend only invokes the script; all flutter/
    // toolchain logic lives in those scripts (build-android.ps1 / build-android.sh),
    // which write the APK to output/<store>-release.apk.
    private async Task RunBuildAsync(string mobileRoot, string store, BuildJob job)
    {
        int exit;
        if (OperatingSystem.IsWindows())
        {
            var script = Path.Combine(mobileRoot, "build-android.ps1");
            if (!File.Exists(script)) throw new FileNotFoundException($"Missing {script}");
            exit = await RunProcessAsync(
                "powershell.exe",
                new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Store", store },
                mobileRoot, job);
        }
        else
        {
            var script = Path.Combine(mobileRoot, "build-android.sh");
            if (!File.Exists(script)) throw new FileNotFoundException($"Missing {script}");
            exit = await RunProcessAsync("bash", new[] { "build-android.sh", store }, mobileRoot, job);
        }

        if (exit != 0) { Finish(job, "failed", null, $"Build failed (exit {exit})."); return; }

        // The build script copies the APK here; publish it under /downloads.
        var apk = Path.Combine(mobileRoot, "output", $"{store}-release.apk");
        if (!File.Exists(apk)) { Finish(job, "failed", null, "Build finished but no APK was found in output/."); return; }

        Directory.CreateDirectory(DownloadsDir);
        var dest = Path.Combine(DownloadsDir, $"{store}-release.apk");
        File.Copy(apk, dest, overwrite: true);
        job.Add(new BuildEvent("log", Line: $"✓ APK ready: {store}-release.apk"));

        Finish(job, "succeeded", $"{_publicBaseUrl}/downloads/{store}-release.apk", "Build succeeded.");
    }

    private static async Task<int> RunProcessAsync(string fileName, string[] args, string workingDir, BuildJob job)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDir,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) job.Add(new BuildEvent("log", Line: e.Data)); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) job.Add(new BuildEvent("log", Line: e.Data)); };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        return proc.ExitCode;
    }

    private static void Finish(BuildJob job, string status, string? apkUrl, string message)
    {
        job.Status = status;
        job.ApkUrl = apkUrl;
        job.Add(new BuildEvent("done", Status: status, ApkUrl: apkUrl, Message: message));
        job.Completed = true;
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

    private static string Sanitize(string store) =>
        new string((store ?? "").ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
}
