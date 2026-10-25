using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

public sealed class ReverseCounterTransactionInputModel
{
    [Required]
    public Guid TransactionId { get; set; }

    public bool IsRefund { get; set; }

    [Required, StringLength(250, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}
