# Resuming this Claude Code chat from the project

This conversation's full history has been copied into this project's session store so
you can continue it with context intact.

## How to resume
1. Open a terminal **in this project folder**:
   `C:\Users\PMLS\Desktop\Projects\shopify-saas-Core`
2. Run Claude Code with resume:
   ```
   claude --resume
   ```
   (or `claude -r`)
3. Pick the session from the list — it's the AppForge / Shopify storefront-token
   discussion (session id `a2fb3c32-36f0-4ffd-afb4-6ebe69464f11`).

To jump straight to it without the picker:
```
claude --resume a2fb3c32-36f0-4ffd-afb4-6ebe69464f11
```

## What was copied
- **Chat transcript** → `~/.claude/projects/C--Users-PMLS-Desktop-Projects-shopify-saas-Core/`
  (the original stays under the old `C--Users-PMLS` key; this is a copy, with the internal
  `cwd` rewritten to this project path).
- **Architecture doc** → `docs/AppForge-Architecture.md`
- **Flow diagram** → `docs/AppForge-Flow.excalidraw` (open at excalidraw.com → File → Open)

## Project decisions so far (summary)
- **AppForge** = platform that generates white-label native mobile apps for Shopify merchants.
- **Onboarding:** OAuth (Public app) → backend gets a scoped Admin API token.
- **Tokens:** Admin token (backend, secret) → mints ONE Storefront token (in app, public) →
  Customer access token (per shopper, via Customer Account API for register/login).
- **Ordering:** Storefront Cart API → `cart.checkoutUrl` → Shopify hosted checkout (no PCI).
- **Verified constraints:** token minting needs a paid-plan store; Customer Account API needs
  the Headless channel enabled; old Checkout API is dead (Cart API is the only path).
- **Still open:** distribution model (per-merchant app vs. multi-tenant container),
  guest-only vs. customer accounts for v1, digital-goods/Apple-IAP question, backend stack.
