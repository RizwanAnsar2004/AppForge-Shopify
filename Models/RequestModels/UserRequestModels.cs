using shopify_saas_Core.Constants.Enums;

namespace shopify_saas_Core.Models.RequestModels;

public sealed class CreateUserRequest
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public UserTypeEnum UserType { get; set; } = UserTypeEnum.Merchant;
}

public sealed class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}
