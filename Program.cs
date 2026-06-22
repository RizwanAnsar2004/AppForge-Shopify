using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using shopify_saas_Core.Helpers;
using shopify_saas_Core.Options;
using shopify_saas_Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<ShopifyOptions>(
    builder.Configuration.GetSection(ShopifyOptions.SectionName));
builder.Services.Configure<AppForgeOptions>(
    builder.Configuration.GetSection(AppForgeOptions.SectionName));

// Single outbound-HTTP choke point (typed HttpClient).
builder.Services.AddHttpClient<ApiCallerHelper>();

// Services depend on ApiCallerHelper for all external calls.
builder.Services.AddScoped<ShopifyOAuthService>();
builder.Services.AddScoped<ShopifyProductService>();
builder.Services.AddScoped<ShopifyStorefrontTokenService>();
builder.Services.AddScoped<ShopifyCustomerService>();

// Drives the mobile-app build pipeline; singleton so jobs survive across requests.
builder.Services.AddSingleton<AppBuildService>();

// Let the Vite dev server (npm run dev) reach the API + SSE stream during development.
const string DevCors = "builder-dev";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p =>
    p.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Fail fast: required Shopify options must be present at startup.
var shopify = app.Services.GetRequiredService<IOptions<ShopifyOptions>>().Value;
if (string.IsNullOrWhiteSpace(shopify.ApiKey)) throw new Exception("Shopify:ApiKey is required.");
if (string.IsNullOrWhiteSpace(shopify.ApiSecret)) throw new Exception("Shopify:ApiSecret is required.");
if (string.IsNullOrWhiteSpace(shopify.Scopes)) throw new Exception("Shopify:Scopes is required.");
if (string.IsNullOrWhiteSpace(shopify.AppUrl)) throw new Exception("Shopify:AppUrl is required.");
if (string.IsNullOrWhiteSpace(shopify.ApiVersion)) throw new Exception("Shopify:ApiVersion is required.");

// Behind ngrok in dev, TLS is already terminated — don't force an in-app HTTPS redirect.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCors);
}

// Serve the AppForge builder SPA (built into wwwroot) at the project root.
// Controller routes (e.g. /auth/*) are matched first; the SPA fallback only
// catches paths that don't map to a controller or a static file.
app.UseDefaultFiles();
app.UseStaticFiles();

// Builder-uploaded images and finished APKs live outside wwwroot (which the SPA
// build wipes) and are served at /uploads and /downloads for the device to fetch.
ServeFolder(app, "Uploads", "/uploads");
ServeFolder(app, "Downloads", "/downloads");

app.UseAuthorization();
app.MapControllers();

// Any unmatched route returns the SPA shell so client-side routing works.
app.MapFallbackToFile("index.html");

app.Run();

// Serve a content-root subfolder under a request path, creating it if missing.
static void ServeFolder(WebApplication app, string folder, string requestPath)
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), folder);
    Directory.CreateDirectory(path);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(path),
        RequestPath = requestPath,
    });
}
