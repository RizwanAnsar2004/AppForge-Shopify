using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using shopify_saas_Core.Constants;
using shopify_saas_Core.Constants.Enums;
using shopify_saas_Core.Data;
using shopify_saas_Core.Data.Entities;
using shopify_saas_Core.Helpers;
using shopify_saas_Core.Models.RequestModels;
using shopify_saas_Core.Options;

namespace shopify_saas_Core.Services.DBServices;

public class AuthService
{
    private readonly AppDbContext db;
    private readonly AuthOptions _authOptions;

    public AuthService(AppDbContext db, IOptions<AuthOptions> authOptions)
    {
        this.db = db;
        _authOptions = authOptions.Value;
    }

    public async Task RegisterAsync(string email)
    {
        if (!AuthHelpers.IsValidEmail(email))
            throw new Exception("Invalid email address.");

        var entry = new OtpVerification
        {
            Email = email.Trim().ToLower(),
            Otp = AppConstants.OtpCode,
            FlowType = FlowTypeEnum.Registration,
            Status = OtpStatusEnum.Pending,
            CreatedOn = DateTime.UtcNow,
        };

        db.OtpVerifications.Add(entry);
        await db.SaveChangesAsync();
    }

    public async Task<User> CreateUserAsync(CreateUserRequest request)
    {
        if (!AuthHelpers.IsValidEmail(request.Email))
            throw new Exception("Invalid email address.");

        var normalized = request.Email.Trim().ToLower();

        if (await db.Users.AnyAsync(u => u.Email == normalized))
            throw new Exception("A user with this email already exists.");

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = normalized,
            Password = AuthHelpers.HashPassword(request.Password, _authOptions.BcryptWorkFactor),
            UserType = request.UserType,
            Status = UserStatusEnum.Active,
            CreatedOn = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<User> LoginAsync(LoginRequest request)
    {
        var normalized = request.Email.Trim().ToLower();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized)
            ?? throw new Exception("Invalid email or password.");

        if (!AuthHelpers.VerifyPassword(request.Password, user.Password))
            throw new Exception("Invalid email or password.");

        if (user.Status != UserStatusEnum.Active)
            throw new Exception("Account is not active.");

        return user;
    }

    public async Task<bool> VerifyOtpAsync(string email, string otp, FlowTypeEnum flowType)
    {
        var expiryThreshold = DateTime.UtcNow.AddMinutes(-_authOptions.OtpExpiryMinutes);

        var record = await db.OtpVerifications
            .Where(o => o.Email == email.Trim().ToLower()
                     && o.FlowType == flowType
                     && o.CreatedOn >= expiryThreshold)
            .OrderByDescending(o => o.CreatedOn)
            .FirstOrDefaultAsync();

        if (record is null || record.Otp != otp) return false;

        record.Status = OtpStatusEnum.Verified;
        await db.SaveChangesAsync();
        return true;
    }
}
