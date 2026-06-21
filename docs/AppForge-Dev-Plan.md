# AppForge — Developer Build Plan

**Product:** AppForge — a SaaS that generates customized native mobile apps for Shopify
merchants. A merchant connects their store in one click; AppForge produces a branded mobile
app where customers browse products, register/sign in, and place orders.

**Backend project:** `shopify-saas-Core` (.NET 10 Web API)
**Status:** Pre-build (architecture + decisions locked)
**Last updated:** 2026-06-19

---

## 1. Locked Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **OAuth via a Shopify Public app** | One-click merchant install; yields a scoped Admin API token (foundation for integrations). Never exposes merchant credentials. |
| 2 | **Direct HTTP calls (no SDK / ShopifySharp)** | Shopify APIs are plain HTTPS (OAuth = POST, Admin/Storefront = GraphQL POST, webhooks = inbound POST). Keeps dependencies thin. |
| 3 | **One Storefront token per merchant**, minted server-side | Covers browse + cart + checkout. Delivered to the app at **runtime** (not hardcoded) so it can rotate without an App Store resubmit. |
| 4 | **Hosted checkout (webview)** for shopper payments | Shopify owns payment + PCI. Cart built natively via Storefront Cart API → `cart.checkoutUrl` → webview. |
| 5 | **Customer registration IN v1** via Customer Account API | Shopper register/sign in (passwordless), order history, profile, addresses. Requires the merchant to enable the **Headless channel**. |
| 6 | **No merchant billing in v1 (free app)** | No Billing API. Upgrade path: free now → Billing API later (NOT external billing, if App-Store-listed). |

### The three tokens (keep these straight)
- **Admin API token** — backend only, **SECRET**, encrypted at rest. Integrations + mints the Storefront token.
- **Storefront token** — lives in the app, public/low-privilege. Browse + cart + checkout.
- **Customer access token** — per shopper, issued by the Customer Account API (register/login).

---

## 2. End-to-End Flow

```
① MERCHANT ONBOARDING (one-time)
   Merchant clicks Install → Shopify OAuth (approve scopes)
     → AppForge backend: exchange code → Admin API token (encrypt + store)
     → mint ONE Storefront token (storefrontAccessTokenCreate)
     → register webhooks
② APP PROVISIONING
   App launches → GET /api/config/{shop} → { branding, Storefront token, Customer Account API client cfg }
③ CUSTOMER EXPERIENCE
   Browse (Storefront token, no login)
   Register / Sign in ⇄ Customer Account API (OAuth + PKCE) → Customer access token
   Logged in → order history / profile / addresses (Customer Account API)
   Add to cart (Storefront Cart API, buyerIdentity = customer)
   Checkout → cart.checkoutUrl → hosted checkout webview (prefilled if logged in)
```

(See `docs/AppForge-Flow.excalidraw` for the visual diagram and `docs/AppForge-Architecture.md`
for the architecture write-up.)

---

## 3. Tech Stack

| Layer | Choice |
|-------|--------|
| Backend | .NET 10 Web API (`shopify-saas-Core`), `IHttpClientFactory` typed clients, `System.Text.Json` |
| Data | EF Core + PostgreSQL (or SQL Server) |
| Secrets | ASP.NET Data Protection / cloud KMS; user-secrets locally |
| Mobile | React Native (Expo + EAS) — best fit for dynamic config + OTA updates |
| Hosting | Azure App Service / container (public HTTPS required by Shopify) |

### Direct-HTTP responsibilities you own (would-be SDK features)
1. **OAuth HMAC verification** — HMAC-SHA256 of the sorted query string with the API secret; constant-time compare.
2. **Webhook HMAC verification** — HMAC-SHA256 of the **raw request body** vs `X-Shopify-Hmac-Sha256` (base64). *Read the raw body BEFORE model binding consumes the stream — classic ASP.NET gotcha.*
3. **OAuth code exchange** — `POST https://{shop}/admin/oauth/access_token`.
4. **GraphQL cost-based throttling** — read `extensions.cost.throttleStatus`; back off / retry on `THROTTLED`.
5. **API version pinning + retries** — version in URL; Polly (or simple retry) for transient 5xx.

### Endpoints hit directly
- Authorize: `GET https://{shop}/admin/oauth/authorize?client_id=...&scope=...&redirect_uri=...&state=...`
- Token exchange: `POST https://{shop}/admin/oauth/access_token`
- Admin GraphQL: `POST https://{shop}/admin/api/{version}/graphql.json` (header `X-Shopify-Access-Token`)
- Storefront GraphQL: `POST https://{shop}/api/{version}/graphql.json` (header `X-Shopify-Storefront-Access-Token`)
- Customer Account API: auth/token URLs from the Headless channel config (PKCE)

---

## 4. Phased Roadmap

### Phase 0 — Accounts & setup (½ day)
- Shopify Partner account → development store → create app (capture API key + secret).
- **Enable the Headless channel** on the dev store → capture Customer Account API credentials (client id, auth/token URLs).
- Ensure a **paid/dev test plan** (`storefrontAccessTokenCreate` requires a paid plan).
- **DoD:** credentials + a store ready to test against.

### Phase 1 — Backend foundation (1–2 days)
- Add EF Core + provider. Configure typed `HttpClient`s.
- `appsettings`: `Shopify:ApiKey`, `ApiSecret` (user-secrets), `Scopes`, `AppUrl`.
- Entities: `Merchant` (shopDomain, adminTokenEncrypted, storefrontToken, status, installedAt),
  `AppConfig` (merchantId, branding JSON), `CustomerAuthConfig` (clientId, authUrl, tokenUrl).
- DI + DbContext + first migration. Replace WeatherForecast scaffold.
- **DoD:** app boots, DB migrates, CRUD on `Merchant`.

### Phase 2 — Shopify OAuth / merchant install (2–3 days)
- `AuthController`:
  - `GET /auth/install?shop=` → build authorize URL (with `state`) → redirect.
  - `GET /auth/callback` → **verify HMAC** → exchange `code` → Admin token → encrypt + store.
- Scopes: `unauthenticated_read_product_listings`, `unauthenticated_write_checkouts`,
  `unauthenticated_read_checkouts` + needed Admin scopes (e.g. `read_products`).
- **DoD:** install on dev store completes; encrypted Admin token persisted.

### Phase 3 — Mint Storefront token (1 day)
- After callback, Admin GraphQL `storefrontAccessTokenCreate` → store on `Merchant`.
- Handle paid-plan failure with a clear message.
- **DoD:** each install yields one stored Storefront token.

### Phase 4 — Mandatory webhooks (1–2 days)
- `WebhookController` (raw-body HMAC verify) for: `customers/data_request`, `customers/redact`,
  `shop/redact`, `app/uninstalled` (uninstall → mark inactive + purge tokens).
- Register webhooks on install.
- **DoD:** all four return 200 and verify — required for App Store approval.

### Phase 5 — Runtime config API (1 day)
- `GET /api/config/{shop}` → `{ shopDomain, storefrontToken, branding, customerAccountApi: { clientId, authUrl, tokenUrl } }`.
- Secure per-merchant (signed/keyed) so tokens aren't enumerable.
- **DoD:** authorized request returns the app config.

### Phase 6 — Mobile app foundation (3–5 days)
- Expo RN app: on launch → fetch config → build Storefront GraphQL client (latest API version).
- **DoD:** app launches, loads config, runs a successful `products` query.

### Phase 7 — Browse → Cart → Checkout (4–6 days)
- Browse: product list + detail (Storefront `products`/`collections`).
- Cart: `cartCreate` / `cartLinesAdd` / `cartLinesUpdate`, local cart state.
- Checkout: `cart.checkoutUrl` → in-app webview → Shopify hosted checkout.
- **DoD:** a test order placed from the app appears in the store admin.

### Phase 8 — Customer registration & accounts (1–2 weeks) — v1
- Mobile auth module: Customer Account API OAuth with **PKCE** — register/sign in
  (passwordless email code) → customer access token → **secure storage** (Keychain/Keystore) → refresh.
- Authenticated screens: order history, profile, saved addresses.
- Cart `buyerIdentity` attaches the customer → checkout prefilled.
- Backend: serve per-merchant Customer Account API client config (public client + PKCE
  generally needs no secret; add a thin proxy only if a confidential flow is needed later).
- **DoD:** shopper registers in-app, logs in, sees order history, checks out prefilled.

### Phase 9 — Customization layer (your core product, 1–2 weeks)
- Merchant web dashboard: branding (logo, colors, fonts, home layout, featured collections) → `AppConfig`.
- App renders config dynamically — one binary reflects each merchant's brand.
- **DoD:** dashboard branding change reflects in the app without a rebuild.

### Phase 10 — Distribution (ongoing)
- Deploy backend to Azure (HTTPS, env secrets, managed DB).
- Distribution model: start with a **single configurable container app**; move to
  **per-merchant EAS builds** for true white-label later.
- **DoD:** production backend live; internal TestFlight/Play build runs against it.

### Phase 11 — Compliance & submission
- Privacy policy; GDPR webhooks verified; **(no Billing API in v1 — free app)**.
- Submit to Shopify App Store; submit mobile app to Apple / Google.
- **DoD:** app approved and listed.

---

## 5. This Week's Critical Path
**Phase 0 (incl. Headless enable) → 1 → 2 → 3.** Proves one-click install + a live Storefront
token — the single milestone that de-risks the whole product. Phase 8 (registration) is the
largest chunk; it sits cleanly after the browse/cart core works.

## 6. Key Risks
- **Headless channel per merchant** — registration isn't fully zero-touch; the merchant must
  enable the Headless channel. Design onboarding to detect + guide this.
- **Token minting needs a paid plan** — affects only your own testing, not real merchants.
- **HMAC correctness (OAuth + webhooks)** — the only security-critical hand-rolled pieces; the
  raw-body requirement for webhooks is the classic ASP.NET pitfall.
- **Monetization upgrade path** — free now → Billing API later keeps App Store eligibility;
  external billing would force you off the App Store.

## 7. Verification (per phase)
- **OAuth:** complete install on the dev store; confirm encrypted Admin token in DB.
- **Storefront token:** confirm one token stored per merchant; query `products` with it.
- **Webhooks:** trigger from Partner dashboard; confirm 200 + signature validation.
- **App E2E:** browse → add to cart → checkout → confirm order in admin.
- **Registration:** register a shopper → log in → load order history → prefilled checkout.
