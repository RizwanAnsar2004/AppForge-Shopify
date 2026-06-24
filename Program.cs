using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using shopify_saas_Core.Data;
using shopify_saas_Core.Helpers.Shopify;
using shopify_saas_Core.Options;
using shopify_saas_Core.Services.AppBuilder;
using shopify_saas_Core.Services.DBServices;
using shopify_saas_Core.Services.Shopify;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<ShopifyOptions>(
    builder.Configuration.GetSection(ShopifyOptions.SectionName));
builder.Services.Configure<AuthOptions>(
    builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<AppForgeOptions>(
    builder.Configuration.GetSection(AppForgeOptions.SectionName));

builder.Services.AddHttpClient<ApiCallerHelper>();

builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped<ShopifyOAuthService>();
builder.Services.AddScoped<ShopifyProductService>();
builder.Services.AddScoped<ShopifyStorefrontTokenService>();
builder.Services.AddScoped<ShopifyCustomerService>();

builder.Services.AddSingleton<AppBuildService>();

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

app.UseDefaultFiles();
app.UseStaticFiles();

ServeFolder(app, "Uploads", "/uploads");
ServeDownloads(app);

app.UseAuthorization();
app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();

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

static void ServeDownloads(WebApplication app)
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "Downloads");
    Directory.CreateDirectory(path);
    var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
    contentTypes.Mappings[".apk"] = "application/vnd.android.package-archive";
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(path),
        RequestPath = "/downloads",
        ContentTypeProvider = contentTypes,
    });
}
