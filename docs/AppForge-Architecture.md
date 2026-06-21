# AppForge — Technical Architecture & Flow Document

**Product:** AppForge — a platform that generates customized native mobile apps for
Shopify merchants. A merchant connects their store; AppForge produces a ready-to-ship
mobile app where customers browse products and place orders.

**Status:** Discovery / architecture (pre-build)
**Date:** 2026-06-18

---

## 1. Executive Summary

AppForge lets a Shopify merchant connect their store in a **single click (OAuth)**.
AppForge's backend receives a scoped **Admin API token**, sets up any integrations,
and mints **one Storefront access token** per merchant. The generated mobile app fetches
its configuration (branding + Storefront token) from the AppForge backend at runtime and
then calls the **Shopify Storefront API directly** for browsing, cart, and checkout.
Payment runs through **Shopify's hosted checkout**, so AppForge carries **no PCI liability**.

Feasibility: **confirmed.** The entire browse → cart → order path needs only one public
Storefront token, and OAuth never exposes merchant account credentials.

---

## 2. The Authoritative Flow

```
┌──────────────┐   1. Click "Install"        ┌────────────────────────────┐
│   Merchant   │ ──────────────────────────▶ │   Shopify OAuth (authorize) │
└──────────────┘                             └────────────┬───────────────┘
       ▲                                                  │ 2. approve scopes
       │ 6. app is ready                                  ▼
┌──────┴───────────────┐   3. callback(code)   ┌────────────────────────────┐
│  AppForge Backend    │ ◀──────────────────── │  Shopify                    │
│  (public HTTPS)      │ ── exchange code ───▶  │  → Admin API access token   │
│                      │ ◀── Admin token ────── │                            │
│  • store Admin token │                       └────────────────────────────┘
│  • set up integrations (Admin API, backend only)
│  • mint ONE Storefront token (storefrontAccessTokenCreate)
│  • store per-merchant config + token
└──────┬───────────────┘
       │ 4. app fetches config at runtime (branding + Storefront token)
       ▼
┌──────────────────────────────┐   5. direct calls   ┌─────────────────────┐
│  Merchant's Mobile App        │ ──────────────────▶ │  Shopify Storefront │
│  • Browse  (Storefront API)   │                     │  API                │
│  • Cart    (Storefront Cart)  │ ◀────────────────── │                     │
│  • Checkout → cart.checkoutUrl │  opens webview ──▶  │  Hosted Checkout    │
└──────────────────────────────┘                     │  (payment + PCI)    │
                                                      └─────────────────────┘
```

**Step detail**
1. Merchant clicks **Install** (one button) → redirected to Shopify authorize URL with
   AppForge's `client_id` + requested scopes.
2. Merchant approves. (Approval ≠ account access — no password/credentials shared.)
3. Shopify redirects to AppForge's `/auth/callback` with a `code`; backend exchanges it
   for a permanent **Admin API token** (verified via HMAC).
4. Backend stores the Admin token, runs any integration setup, and mints **one Storefront
   token** via the Admin GraphQL `storefrontAccessTokenCreate` mutation.
5. The mobile app fetches its config (branding + Storefront token) from the AppForge
   backend **at runtime**, then calls the Shopify **Storefront API directly**.
6. Checkout hands off `cart.checkoutUrl` to Shopify's hosted checkout in a webview; the
   order is created in the merchant's Shopify admin automatically.

---

## 3. The Two Tokens (critical distinction)

| Token | Where it lives | What it can do | Secrecy |
|-------|----------------|----------------|---------|
| **Admin API token** | **Backend only** | Integrations: orders, inventory, fulfillment, discounts, webhooks. Mints the Storefront token. | **Secret** — never ships to the app |
| **Storefront API token** | **Inside the app** (delivered at runtime) | Browse products, manage cart, create checkout. Cannot read orders/PII/settings. | Public by design — safe to embed |

**There is only ONE Storefront token** — it covers browsing, cart, *and* checkout. There
is no separate "cart token." A cart has a `cartId`, but that is an identifier returned per
cart, not a credential.

---

## 4. Key Design Decisions

### 4.1 OAuth (Public app) — DECIDED
- **Why:** A single click yields a scoped **Admin token**, the foundation for current and
  future integrations (order webhooks, inventory sync, fulfillment, discounts). Manual
  token paste cannot provide this.
- **App type:** Public app (multi-merchant, Shopify App Store eligible). Not a custom app
  (single-store only).
- **No account access:** OAuth never exposes the merchant's password or account.

### 4.2 Deliver the Storefront token at runtime — RECOMMENDED (not hardcoded)
- **Do not** bake the Storefront token into the app binary. If it did, rotating or
  revoking a token would require an App Store resubmission.
- **Instead:** the app fetches a small config payload (theme, merchant domain, Storefront
  token) from the AppForge backend on launch. Same end result (frontend → Shopify direct),
  but tokens, branding, and merchant status are updatable without a new app release.

### 4.3 Hosted checkout — DECIDED
- Cart is built natively via the Storefront Cart API; the final payment step opens
  Shopify's **hosted checkout** in a webview. Shopify owns payment, tax, shipping,
  discounts, and **PCI compliance**. Fully native checkout is gated and would shift PCI
  liability to AppForge — **not pursued**.

### 4.4 Request least-privilege scopes
- Storefront-power scopes: `unauthenticated_read_product_listings`,
  `unauthenticated_write_checkouts`, `unauthenticated_read_checkouts`.
- Admin scopes added **per integration as needed** (`read_products`, `read_orders`,
  `read_inventory`, ...). Merchants re-consent when scopes expand.

---

## 5. Backend Components Required

- **Public HTTPS service** with `/install` and `/auth/callback` routes (Shopify provides
  official libraries — `@shopify/shopify-app-js` for Node, plus a Remix template — that
  handle most boilerplate).
- **HMAC verification** on OAuth callbacks and all webhooks (security requirement).
- **Token generation:** Admin GraphQL `storefrontAccessTokenCreate`. **Dependency:** the
  app must be granted the `unauthenticated_*` scopes — a Storefront token inherits its
  scopes from the app, and minting **fails** if the app has none. **Note:** this mutation
  requires the store to be on a **paid Shopify plan** (does not run on a plain dev store).
- **API version:** call the **latest stable** Shopify API version (versioned quarterly);
  pin to a current version string and bump on Shopify's schedule.
- **Per-merchant store:** Admin token + Storefront token + app config, encrypted at rest.
- **Config endpoint:** serves runtime config (branding + Storefront token) to each app.
- **Mandatory webhooks:** GDPR (`customers/data_request`, `customers/redact`,
  `shop/redact`) for App Store approval; `app/uninstalled` to revoke/clean up tokens.

---

## 6. Benefits

- **Frictionless onboarding** — one click; no copy-pasting tokens, no credential sharing.
- **No PCI liability** — payment stays on Shopify's hosted checkout.
- **No merchant account access** — OAuth grants only approved scopes; AppForge never sees
  passwords.
- **Future-proof** — the Admin token enables an open-ended roadmap of integrations.
- **Operable in production** — runtime config delivery means tokens/branding rotate
  without App Store resubmissions.
- **Proven model** — mirrors Tapcart, Vajro, Shopney.

## 7. Trade-offs & Risks

| Area | Trade-off / Risk | Mitigation |
|------|------------------|-----------|
| Checkout UX | Final payment screen is Shopify's web checkout, not native | Native browse/cart; brand the hosted checkout; seamless webview handoff |
| App Store (digital goods) | Digital goods/subscriptions trigger Apple IAP (~30%) | Confirm merchant goods type; restrict to physical, or plan IAP for digital |
| Merchant action required | A token-less flow is impossible; merchant must install once | OAuth makes this a single click — minimal friction |
| Token exposure | Storefront token is public/visible in app traffic | By design it is low-privilege (no orders/PII); rotate via runtime config if abused |
| Compliance burden | GDPR + uninstall webhooks mandatory for App Store | Implement upfront using official libraries |
| Scope creep | Over-requesting Admin scopes hurts trust/review | Least-privilege; add scopes per integration with re-consent |
| App Store review | Pure website wrappers get rejected (guideline 4.2) | AppForge ships genuine native browse/cart, not a webview shell |

---

## 8. Open Decisions

1. ~~Onboarding path~~ — **DECIDED: OAuth (Public app).**
2. **Distribution model:** one app per merchant (their own App Store listing) vs. a single
   multi-tenant container app. Most affects build pipeline and config delivery.
3. **Shopper accounts:** guest checkout only (v1) vs. Customer Account API (order history,
   login — merchant must enable Headless channel).
4. **Merchant goods type:** confirm physical-only vs. digital (drives Apple IAP analysis).
5. **Backend stack:** language/framework for the OAuth service (Node + official Shopify
   library recommended).

---

## 9. Proof-of-Concept (recommended before full build)

1. Create a Shopify **development store** (free via Partners account). **Caveat:**
   `storefrontAccessTokenCreate` requires a **paid plan**, so for the token-minting test
   use a paid/granted test store, OR create the Storefront token **manually in the admin
   UI** to exercise the app side while building OAuth separately.
2. Register a **Public app** with the `unauthenticated_*` scopes; implement `/install` +
   `/auth/callback` with the official library; complete OAuth and capture the Admin token.
3. Call Admin `storefrontAccessTokenCreate` to mint a Storefront token (paid-plan store).
4. Using only that Storefront token against
   `https://{shop}.myshopify.com/api/{latest-version}/graphql.json`
   (header `X-Shopify-Storefront-Access-Token`):
   - query `products` (browse),
   - `cartCreate` + `cartLinesAdd` (cart),
   - read `checkoutUrl`, open it, complete a test order and confirm it appears in admin.
5. This validates the full OAuth → token → browse → cart → order chain — with no merchant
   account access — before committing to the app-builder build.
