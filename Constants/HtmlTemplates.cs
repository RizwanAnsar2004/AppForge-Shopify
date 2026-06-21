namespace shopify_saas_Core.Constants;

// Tokens: {shop}, {token}, {storefrontToken}, {customerToken}
public static class HtmlTemplates
{
    public const string InstallSuccess =
        """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>AppForge — Installed</title>
          <style>
            body { margin: 0; min-height: 100vh; display: flex; align-items: center; justify-content: center;
                   font-family: -apple-system, Segoe UI, Roboto, sans-serif; background: #0b1020; color: #e8ecf1; }
            .card { background: #151b2e; padding: 48px 40px; border-radius: 16px; text-align: center;
                    box-shadow: 0 20px 60px rgba(0,0,0,.4); max-width: 480px; }
            h1 { margin: 0 0 12px; font-size: 28px; }
            p { margin: 8px 0; line-height: 1.5; color: #aeb7c6; }
            .shop { color: #6ee7b7; font-weight: 600; }
            .label { font-size: 13px; color: #7c8699; margin-top: 20px; margin-bottom: 6px; }
            .token { display: block; user-select: all; word-break: break-all;
                     font-family: ui-monospace, Menlo, Consolas, monospace; font-size: 13px;
                     background: #0b1020; color: #6ee7b7; padding: 12px 14px; border-radius: 8px;
                     border: 1px solid #2a3350; }
          </style>
        </head>
        <body>
          <div class="card">
            <h1>🎉 Congratulations! 🎉</h1>
            <p>AppForge was successfully installed for <span class="shop">{shop}</span>.</p>
            <p>Your store is now connected. Keep these tokens safe.</p>
            <p class="label">Admin API access token (secret — backend only):</p>
            <code class="token">{token}</code>
            <p class="label">Storefront API access token (public — used in the app):</p>
            <code class="token">{storefrontToken}</code>
            <p class="label">Customer access token (per-shopper, test):</p>
            <code class="token">{customerToken}</code>
            <p>You can close this window.</p>
          </div>
        </body>
        </html>
        """;
}
