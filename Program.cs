using Microsoft.Extensions.Options;
using shopify_saas_Core.Helpers;
using shopify_saas_Core.Options;
using shopify_saas_Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<ShopifyOptions>(
    builder.Configuration.GetSection(ShopifyOptions.SectionName));

// Single outbound-HTTP choke point (typed HttpClient).
builder.Services.AddHttpClient<ApiCallerHelper>();

// Services depend on ApiCallerHelper for all external calls.
builder.Services.AddScoped<ShopifyOAuthService>();
builder.Services.AddScoped<ShopifyProductService>();
builder.Services.AddScoped<ShopifyStorefrontTokenService>();
builder.Services.AddScoped<ShopifyCustomerService>();

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

app.UseAuthorization();
app.MapControllers();
app.Run();
