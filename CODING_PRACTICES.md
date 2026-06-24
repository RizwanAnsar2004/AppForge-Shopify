# Coding Practices

## Exceptions
- Always use `throw new Exception(...)` — no `InvalidOperationException`, `ArgumentException`, or any other exception type.

## Comments
- No comments in code. No `///` doc comments unless explicitly asked.

## Enums
- All enums live in `Constants/Enums/`.
- File names and class names both end with `Enum` — e.g., `BuildStatusEnum.cs` / `public enum BuildStatusEnum`.
- Enums are stored as integers in the database.

## Models
- Split into two folders: `Models/RequestModels/` and `Models/ResponseModels/`.
- Group related models in one file named `[Domain]RequestModels.cs` or `[Domain]ResponseModels.cs`.
  - e.g., all user request models → `UserRequestModels.cs`
  - e.g., all build request models → `BuildRequest.cs`
- Always create a request model (DTO) for method inputs — never use individual primitive parameters for multi-field inputs.

## Services
- `Services/Shopify/` — Shopify API services.
- `Services/AppBuilder/` — mobile build pipeline services.
- `Services/DBServices/` — all database-touching services (EF Core).

## Helpers
- `Helpers/Shopify/` — Shopify-specific helpers.
- `Helpers/AppBuilder/` — build-specific helpers.
- Single-concern helpers that don't belong to a domain go as flat files directly in `Helpers/` — e.g., `AuthHelpers.cs`. No extra subfolder.

## Entity Framework Core
- Use **Data Annotations** on entity classes — no Fluent API / `OnModelCreating` for table configuration.
- Use `[Table]`, `[Key]`, `[DatabaseGenerated]`, `[Required]`, `[MaxLength]`, `[Index]`, `[ForeignKey]` attributes directly on entities.
- `AppDbContext` only declares `DbSet<T>` properties — no configuration code inside it.

## Database Users / Connection Strings
- Two Postgres users: `appforge_migrations` (DDL, migrations only) and `appforge_app` (DML, runtime only).
- `appsettings.json` → `ConnectionStrings:Default` (app user).
- `appsettings.Development.json` → `ConnectionStrings:Migrations` (migration user, dev only).
- `AppDbContextFactory` picks up `Migrations` connection at design time so `dotnet ef` commands use the migration user automatically.

## Configuration / Options
- Use the Options pattern (`IOptions<T>`) for all config values — never hardcode config in services.
- Each options class lives in `Options/` and declares a `SectionName` constant.
- Dev-only config goes in `appsettings.Development.json`, not `appsettings.json`.

## Constants
- Non-enum constants live in `Constants/AppConstants.cs` as a static class.

## Build Pipeline
- Never write Flutter/shell commands inline in C# — always invoke the existing scripts (`build-android.ps1` on Windows, `build-android.sh` on Linux).
- Background builds use `Task.Run` + `ConcurrentDictionary` job store with SSE log streaming.

## Frontend (React / Vite)
- Enums mirror the backend exactly, with the same `Enum` suffix — e.g., `BuildStatusEnum`.
- All related models grouped in one file following the same domain-file convention as the backend.
- Build output goes to `../shopify-saas-Core/wwwroot` — served directly by the .NET app.
