using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

public sealed class RefundMoneyOrderInputModel
{
    [Required]
    public Guid MoneyOrderId { get; set; }

    [Required, StringLength(250, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}
