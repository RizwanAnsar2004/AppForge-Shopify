# AppForge — Shopify SaaS Backend

A .NET 10 Web API that generates customized native mobile apps for Shopify merchants.
A merchant connects their store in one click (OAuth); AppForge mints the tokens the
mobile app needs (Storefront token) and exposes per-merchant config so the generated
app can browse products, register customers, and place orders.

## Stack
- .NET 10 Web API
- Direct HTTP calls to Shopify (no SDK) via a single `ApiCallerHelper`
- Options pattern (`ShopifyOptions`) with fail-fast startup validation

## Configuration

Secrets are **not** committed. Provide them via user-secrets (Development):

```bash
dotnet user-secrets set "Shopify:ApiKey" "<your-client-id>"
dotnet user-secrets set "Shopify:ApiSecret" "<your-client-secret>"
```

Non-secret settings live in `appsettings.json` (`Shopify:Scopes`, `Shopify:AppUrl`,
`Shopify:ApiVersion`).

## Run

```bash
dotnet run --launch-profile http
```

Shopify must reach the backend over public HTTPS during OAuth/webhooks — in dev this is
done with a tunnel (e.g. ngrok) pointed at the local HTTP port.

## OAuth endpoints
- `GET /auth/install?shop=<name>.myshopify.com` — start the merchant install
- `GET /auth/callback` — exchanges the code for the Admin API token, seeds test products
  if the store is empty, and mints the Storefront token

> Note: HMAC verification is currently omitted for local development and must be added
> back before production / App Store submission.

## Project layout
```
Constants/   Shopify URL strings, seed product data, HTML templates
Helpers/     ApiCallerHelper (outbound HTTP), ShopifyUrlHelper, GraphQlHelper
Options/     ShopifyOptions (bound from the "Shopify" config section)
Services/    OAuth, products, storefront token, customer token
Controllers/ AuthController
```
