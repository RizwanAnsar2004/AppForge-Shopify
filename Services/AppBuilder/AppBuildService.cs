using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using shopify_saas_Core.Constants;
using shopify_saas_Core.Helpers.AppBuilder;
using shopify_saas_Core.Models.AppBuilder;
using shopify_saas_Core.Options;

namespace shopify_saas_Core.Services.AppBuilder;

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

    public BuildJob? Get(string id) => _jobs.TryGetValue(id, out var job) ? job : null;

    public BuildJob Start(BuildRequest request)
    {
        var store = Sanitize(request.Store);
        if (store.Length == 0) throw new Exception("Invalid store name.");

        var job = new BuildJob();
        _jobs[job.Id] = job;
        _ = Task.Run(() => RunAsync(job, store, request));
        return job;
    }

    private async Task RunAsync(BuildJob job, string store, BuildRequest request)
    {
        try
        {
            job.Status = BuildStatus.Running;
            job.Log($"▶ Build started for store '{store}'");

            var mobileRoot = Path.GetFullPath(_opts.MobileProjectPath, Directory.GetCurrentDirectory());
            if (!Directory.Exists(mobileRoot))
                throw new Exception($"Mobile project not found at {mobileRoot}");

            await WriteConfigAsync(mobileRoot, store, request, job);

            if (_opts.BuildEnabled)
                await BuildApkAsync(mobileRoot, store, job);
            else
                ReportManualBuild(store, job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build failed for {Store}", store);
            job.Log($"✖ {ex.Message}");
            job.Finish(BuildStatus.Failed, null, ex.Message);
        }
    }

    private async Task WriteConfigAsync(string mobileRoot, string store, BuildRequest request, BuildJob job)
    {
        var config = new Dictionary<string, string>(request.Config);

        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        foreach (var img in ImageStore.Save(uploadsRoot, store, request.Images, _publicBaseUrl))
        {
            config[img.ConfigKey] = img.Url;
            job.Log($"✓ Saved {img.ConfigKey} ({img.Bytes} bytes)");
        }

        var configDir = Path.Combine(mobileRoot, "config");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, $"{store}.json");
        await File.WriteAllTextAsync(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        job.Log($"✓ Wrote {Path.GetRelativePath(mobileRoot, configPath)}");
    }

    private async Task BuildApkAsync(string mobileRoot, string store, BuildJob job)
    {
        var (file, args) = BuildScriptCommand(mobileRoot, store);
        var exit = await ProcessRunner.RunAsync(file, args, mobileRoot, job.Log);
        if (exit != 0) { job.Finish(BuildStatus.Failed, null, $"Build failed (exit {exit})."); return; }

        var apk = Path.Combine(mobileRoot, "output", $"{store}-release.apk");
        if (!File.Exists(apk)) { job.Finish(BuildStatus.Failed, null, "Build finished but no APK was found in output/."); return; }

        var downloads = Path.Combine(Directory.GetCurrentDirectory(), "Downloads");
        Directory.CreateDirectory(downloads);
        File.Copy(apk, Path.Combine(downloads, $"{store}-release.apk"), overwrite: true);
        job.Log($"✓ APK ready: {store}-release.apk");

        job.Finish(BuildStatus.Succeeded, $"{_publicBaseUrl}/downloads/{store}-release.apk", "Build succeeded.");
    }

    private static (string file, string[] args) BuildScriptCommand(string mobileRoot, string store)
    {
        if (OperatingSystem.IsWindows())
        {
            var script = Path.Combine(mobileRoot, "build-android.ps1");
            if (!File.Exists(script)) throw new Exception($"Missing {script}");
            return ("powershell.exe",
                new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Store", store });
        }

        var sh = Path.Combine(mobileRoot, "build-android.sh");
        if (!File.Exists(sh)) throw new Exception($"Missing {sh}");
        return ("bash", new[] { "build-android.sh", store });
    }

    private static void ReportManualBuild(string store, BuildJob job)
    {
        var cmd = OperatingSystem.IsWindows()
            ? $".\\build-android.ps1 -Store {store}"
            : $"bash build-android.sh {store}";
        job.Log("ℹ Build pipeline disabled (AppForge:BuildEnabled=false).");
        job.Log("  Build it from the appforge-mobile project:");
        job.Log($"  {cmd}");
        job.Finish(BuildStatus.Succeeded, null, "Config generated. Run the build command shown in the log.");
    }

    private static string Sanitize(string store) =>
        new string((store ?? "").ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
}
