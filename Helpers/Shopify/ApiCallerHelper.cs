using System.Net.Http.Json;

namespace shopify_saas_Core.Helpers.Shopify;

public sealed class ApiCallerHelper
{
    private readonly HttpClient _http;

    public ApiCallerHelper(HttpClient http) => _http = http;

    public async Task<TResponse> PostJsonAsync<TResponse>(
        string url,
        object body,
        IDictionary<string, string>? headers = null,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        ApplyHeaders(request, headers);

        return await SendAsync<TResponse>(request, ct);
    }

    public async Task<TResponse> GetJsonAsync<TResponse>(
        string url,
        IDictionary<string, string>? headers = null,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyHeaders(request, headers);

        return await SendAsync<TResponse>(request, ct);
    }

    private async Task<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>(ct)
               ?? throw new InvalidOperationException($"Empty response from {request.RequestUri}.");
    }

    private static void ApplyHeaders(HttpRequestMessage request, IDictionary<string, string>? headers)
    {
        if (headers is null) return;
        foreach (var (key, value) in headers)
            request.Headers.TryAddWithoutValidation(key, value);
    }
}
