using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using shopify_saas_Core.Constants.Enums;

namespace shopify_saas_Core.Data.Entities;

[Table("OtpVerifications")]
[Index(nameof(Email))]
public sealed class OtpVerification
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int VerificationId { get; set; }

    public required string Otp { get; set; }

    public required string Email { get; set; }

    public OtpStatusEnum Status { get; set; } = OtpStatusEnum.Pending;

    public FlowTypeEnum FlowType { get; set; } = FlowTypeEnum.Registration;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
