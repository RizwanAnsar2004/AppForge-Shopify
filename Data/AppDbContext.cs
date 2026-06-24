using Microsoft.EntityFrameworkCore;
using shopify_saas_Core.Data.Entities;

namespace shopify_saas_Core.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();
}
