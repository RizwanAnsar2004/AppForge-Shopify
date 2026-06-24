using System.Text.RegularExpressions;

namespace shopify_saas_Core.Helpers;

public static class AuthHelpers
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsValidEmail(string email) => EmailRegex.IsMatch(email);

    public static string HashPassword(string password, int workFactor = 11) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor);

    public static bool VerifyPassword(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
