using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using shopify_saas_Core.Constants.Enums;

namespace shopify_saas_Core.Data.Entities;

[Table("Users")]
[Index(nameof(Email), IsUnique = true)]
public sealed class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int UserId { get; set; }

    public required string Name { get; set; } = "";

    public required string Email { get; set; } = "";

    public required string Password { get; set; } = "";

    public UserStatusEnum Status { get; set; } = UserStatusEnum.Inactive;

    public UserTypeEnum UserType { get; set; } = UserTypeEnum.Merchant;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedOn { get; set; }

    public int? UpdatedBy { get; set; }

    [ForeignKey(nameof(UpdatedBy))]
    public User? UpdatedByUser { get; set; }
}
