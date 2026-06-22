using shopify_saas_Core.Constants;

namespace shopify_saas_Core.Services.AppBuilder;

public sealed record BuildEvent(string Type, string? Line = null, BuildStatus? Status = null,
    string? ApkUrl = null, string? Message = null);

public sealed class BuildJob
{
    public string Id { get; } = Guid.NewGuid().ToString("n");
    public BuildStatus Status { get; set; } = BuildStatus.Queued;
    public string? ApkUrl { get; set; }
    public volatile bool Completed;

    private readonly List<BuildEvent> _events = new();
    private readonly object _lock = new();

    public void Add(BuildEvent e)
    {
        lock (_lock) _events.Add(e);
    }

    public void Log(string line) => Add(new BuildEvent("log", Line: line));

    public void Finish(BuildStatus status, string? apkUrl, string message)
    {
        Status = status;
        ApkUrl = apkUrl;
        Add(new BuildEvent("done", Status: status, ApkUrl: apkUrl, Message: message));
        Completed = true;
    }

    public BuildEvent[] Since(int index)
    {
        lock (_lock)
            return index >= _events.Count
                ? Array.Empty<BuildEvent>()
                : _events.GetRange(index, _events.Count - index).ToArray();
    }
}
