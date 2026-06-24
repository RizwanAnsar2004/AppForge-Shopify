# Coding Practices

This document is the authoritative source of truth for all coding conventions in this project.
Every rule here must be followed exactly — do not deviate based on personal preference, common
defaults, or framework conventions that conflict with what is written here.

---

## 1. Exceptions

**Rule:** Always throw `new Exception(message)`. Never use any other exception type.

**Applies to:** All C# code in this project — controllers, services, helpers, everywhere.

```csharp
// CORRECT
throw new Exception("Store name is required.");

// WRONG — do not use these
throw new ArgumentNullException(nameof(store));
throw new InvalidOperationException("Store name is required.");
throw new ArgumentException("Invalid store name.", nameof(store));
```

---

## 2. Comments

**Rule:** Write zero comments. No inline comments, no block comments, no XML doc comments (`///`).

Well-named identifiers are the only documentation needed. If you feel a comment is necessary,
rename the variable or method instead.

```csharp
// WRONG
/// <summary>Validates the email address.</summary>
public static bool IsValidEmail(string email) { ... }

// CORRECT
public static bool IsValidEmail(string email) { ... }
```

---

## 3. Enums

### Location
All enums live in `Constants/Enums/`. One enum per file.

### Naming
Both the **file name** and the **class name** must end with `Enum`.

```
Constants/
  Enums/
    BuildStatusEnum.cs      → public enum BuildStatusEnum
    UserStatusEnum.cs       → public enum UserStatusEnum
    UserTypeEnum.cs         → public enum UserTypeEnum
    OtpStatusEnum.cs        → public enum OtpStatusEnum
    FlowTypeEnum.cs         → public enum FlowTypeEnum
```

### Values
Always assign explicit integer values starting from 0.

```csharp
// CORRECT
public enum UserStatusEnum
{
    Inactive = 0,
    Active = 1,
    Suspended = 2,
}
```

### Database Storage
Enums are stored as integers in the database. Apply `[Column(TypeName = "int")]` or rely on
EF Core's default integer mapping for enum properties — do not store as strings.

### Frontend Mirroring
The React/TypeScript frontend mirrors every backend enum with the exact same name and values,
also using the `Enum` suffix:

```typescript
// src/constants.ts
export enum BuildStatusEnum {
  Queued = 0,
  Running = 1,
  Succeeded = 2,
  Failed = 3,
}
```

---

## 4. Constants

Non-enum constant values (strings, numbers used across the codebase) live in
`Constants/AppConstants.cs` as a single static class.

```csharp
// Constants/AppConstants.cs
namespace shopify_saas_Core.Constants;

public static class AppConstants
{
    public const string OtpCode = "1234";
}
```

When a constant is environment-specific or should be configurable at deploy time, move it to
`appsettings.json` and read it via an Options class instead (see Section 8).

---

## 5. Models

### Folder Split
All models are split into exactly two subfolders:

```
Models/
  RequestModels/    → inputs coming INTO the system (from HTTP body, from service callers)
  ResponseModels/   → outputs going OUT of the system (returned from endpoints or services)
```

### File Naming — Domain Grouping
Related models are grouped into a single file named `[Domain]RequestModels.cs` or
`[Domain]ResponseModels.cs`. Do **not** create one file per model class.

```
Models/
  RequestModels/
    UserRequestModels.cs     → CreateUserRequest, UpdateUserRequest, etc.
    BuildRequest.cs          → BuildRequest, BuildImages, ImagePayload (build domain)
  ResponseModels/
    BuildStartResponse.cs    → BuildStartResponse
```

**When adding a new model:** check if a file for that domain already exists and add the class
there. Only create a new file when starting a completely new domain.

### DTO for Inputs
Never pass multiple related primitive parameters to a method. Always create a request model.

```csharp
// WRONG
public async Task<User> CreateUserAsync(string name, string email, string password, UserTypeEnum userType)

// CORRECT — define CreateUserRequest in Models/RequestModels/UserRequestModels.cs
public async Task<User> CreateUserAsync(CreateUserRequest request)
```

### Return Types from Endpoints
Endpoints must return a typed DTO, never a primitive (`string`, `int`) and never `IActionResult`.

```csharp
// WRONG
public string Start([FromBody] BuildRequest request) => job.Id;

// WRONG
public IActionResult Start([FromBody] BuildRequest request) => Ok(new { jobId = job.Id });

// CORRECT — define BuildStartResponse in Models/ResponseModels/
public BuildStartResponse Start([FromBody] BuildRequest request)
    => new BuildStartResponse { JobId = job.Id };
```

---

## 6. Services

Services are split into domain subfolders under `Services/`:

```
Services/
  Shopify/          → all Shopify API interaction (OAuth, products, storefront tokens, customers)
  AppBuilder/       → mobile APK build pipeline (AppBuildService, BuildJob)
  DBServices/       → all services that touch the database via EF Core (AuthService, etc.)
```

**Rule:** Any service that calls `db.SaveChangesAsync()`, queries `AppDbContext`, or owns a
`DbSet` operation belongs in `Services/DBServices/`. Do not scatter EF Core calls across other
service folders.

---

## 7. Helpers

Helpers are pure static utility classes. They do not have state and are not registered in DI.

```
Helpers/
  Shopify/            → ShopifyUrlHelper, GraphQlHelper, ApiCallerHelper
  AppBuilder/         → ImageStore, ProcessRunner
  AuthHelpers.cs      → flat file, no subfolder (single-concern, no domain folder needed)
```

**Rule for subfolders:** Only create a subfolder when there are multiple helper files for the
same domain. A single helper file goes directly in `Helpers/` as a flat file.

```
// WRONG — unnecessary subfolder for one file
Helpers/Auth/AuthHelpers.cs

// CORRECT
Helpers/AuthHelpers.cs
```

---

## 8. Configuration / Options Pattern

All configurable values must use ASP.NET Core's Options pattern (`IOptions<T>`). No hardcoded
config values inside services or controllers.

### Structure
Each options class lives in `Options/` and must declare a `SectionName` constant:

```csharp
// Options/AuthOptions.cs
namespace shopify_saas_Core.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public int OtpExpiryMinutes { get; set; } = 10;
}
```

### Registration (Program.cs)
```csharp
builder.Services.Configure<AuthOptions>(
    builder.Configuration.GetSection(AuthOptions.SectionName));
```

### Injection into Services
```csharp
public class AuthService
{
    private readonly AuthOptions _authOptions;
    public AuthService(AppDbContext db, IOptions<AuthOptions> authOptions)
    {
        _authOptions = authOptions.Value;
    }
}
```

### appsettings.json
```json
"Auth": {
  "OtpExpiryMinutes": 10
}
```

### Dev vs Production Config
- `appsettings.json` → production / shared values
- `appsettings.Development.json` → values that only apply locally (e.g., migration connection string)

---

## 9. Entity Framework Core

### Data Annotations Only
Configure all entities using **Data Annotations** directly on the entity class.
**Never use Fluent API** (`OnModelCreating`, `IEntityTypeConfiguration<T>`, `modelBuilder.*`).

```csharp
// CORRECT
[Table("Users")]
[Index(nameof(Email), IsUnique = true)]
public sealed class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = "";
}

// WRONG — do not do this
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<User>().HasKey(u => u.UserId);
    modelBuilder.Entity<User>().Property(u => u.Name).HasMaxLength(100);
}
```

### AppDbContext
`AppDbContext` only declares `DbSet<T>` properties. No `OnModelCreating` override, no
configuration code, nothing else.

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();
}
```

### Entity Location
All EF Core entities live in `Data/Entities/`.

### Migrations
Migrations use a separate high-privilege database user (`appforge_migrations`) injected via
`AppDbContextFactory` which reads `ConnectionStrings:Migrations` from config. The runtime API
uses a low-privilege user (`appforge_app`) via `ConnectionStrings:Default`.

---

## 10. Database Users

Two Postgres users exist for this project:

| User | Permissions | Used by |
|------|-------------|---------|
| `appforge_migrations` | Full DDL (CREATE, ALTER, DROP) | `dotnet ef` commands only |
| `appforge_app` | DML only (SELECT, INSERT, UPDATE, DELETE) | Running API |

Connection strings:
- `appsettings.json` → `ConnectionStrings:Default` → `appforge_app`
- `appsettings.Development.json` → `ConnectionStrings:Migrations` → `appforge_migrations`

`AppDbContextFactory` (`Data/AppDbContextFactory.cs`) makes `dotnet ef` automatically pick up
the `Migrations` connection string so you never need `--connection` flags.

---

## 11. Build Pipeline (Mobile APK)

The Flutter APK build is triggered by the .NET backend but **always delegates to shell scripts**.
Never write Flutter or shell commands inline in C# code.

- Windows: invoke `build-android.ps1` via `powershell.exe`
- Linux: invoke `build-android.sh` via `bash`

```csharp
// CORRECT
return ("powershell.exe", new[] { "-File", script, "-Store", store });

// WRONG
Process.Start("flutter", "build apk --release");
```

---

## 12. SSE (Server-Sent Events)

Build progress is streamed from the backend to the React frontend using SSE.

- Endpoint: `GET /api/build/{id}/events` → `Content-Type: text/event-stream`
- Each event is a JSON object framed as `data: {...}\n\n`
- The `status` field in SSE events is the **integer value** of `BuildStatusEnum` (not a string)
- The frontend `EventSource` parses each `msg.data` with `JSON.parse`

---

## 13. Frontend (React / Vite / TypeScript)

- Build output directory: `../shopify-saas-Core/wwwroot` (served by the .NET app as static files)
- Backend base URL comes from `VITE_BACKEND_BASE` env var (empty = same origin)
- All enums mirror backend with the same name and `Enum` suffix
- Model grouping convention mirrors backend: domain-grouped files in `src/types/` or `src/`
- API functions live in `src/api.ts`
- Constants (enums) live in `src/constants.ts`

---

## 14. Program.cs Conventions

- Services registered in this order: controllers → DbContext → options → HttpClient → scoped services → singletons → CORS
- `AddScoped` for request-scoped services (Shopify services, DBServices)
- `AddSingleton` for long-lived stateful services (AppBuildService — holds in-memory job store)
- Static local functions at the bottom of `Program.cs` for middleware setup helpers (e.g., `ServeFolder`, `ServeDownloads`)
