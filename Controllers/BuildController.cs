using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using shopify_saas_Core.Services;

namespace shopify_saas_Core.Controllers;

// Turns a builder config into a local Flutter APK build and streams progress.
[ApiController]
[Route("api/build")]
public sealed class BuildController : ControllerBase
{
    private readonly AppBuildService _builds;

    public BuildController(AppBuildService builds) => _builds = builds;

    // Start a build. Returns a jobId the client subscribes to over SSE.
    [HttpPost]
    public IActionResult Start([FromBody] BuildRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Store))
            return BadRequest(new { message = "Missing 'store'." });

        var job = _builds.Start(request);
        return Ok(new { jobId = job.Id });
    }

    // Server-Sent Events: replays buffered logs, then streams new ones live until done.
    [HttpGet("{id}/events")]
    public async Task Events(string id, CancellationToken ct)
    {
        var job = _builds.Get(id);
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        if (job is null)
        {
            await WriteEvent(new BuildEvent("done", Status: "failed", Message: "Unknown build job."), ct);
            return;
        }

        var sent = 0;
        while (!ct.IsCancellationRequested)
        {
            foreach (var ev in job.Since(sent))
            {
                await WriteEvent(ev, ct);
                sent++;
                if (ev.Type == "done") return;
            }
            if (job.Completed) return;
            await Task.Delay(200, ct);
        }
    }

    private async Task WriteEvent(BuildEvent ev, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(ev, JsonOpts);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);
}
