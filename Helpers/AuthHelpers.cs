using System.Text.RegularExpressions;

namespace shopify_saas_Core.Helpers;

public static class AuthHelpers
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsValidEmail(string email) => EmailRegex.IsMatch(email);
}
