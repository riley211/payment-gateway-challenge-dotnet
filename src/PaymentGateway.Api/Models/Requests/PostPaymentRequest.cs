using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Models.Requests;

public class PostPaymentRequest : IValidatableObject
{
    [Required]
    [StringLength(19, MinimumLength = 14)]
    [RegularExpression("^[0-9]+$", ErrorMessage = "The CardNumber field must contain only digits.")]
    public string? CardNumber { get; set; }

    [Required]
    [Range(1, 12)]
    public int? ExpiryMonth { get; set; }

    [Required]
    public int? ExpiryYear { get; set; }

    [Required]
    [AllowedValues("GBP", "USD", "EUR")]
    public string? Currency { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? Amount { get; set; }

    [Required]
    [StringLength(4, MinimumLength = 3)]
    [RegularExpression("^[0-9]+$", ErrorMessage = "The Cvv field must contain only digits.")]
    public string? Cvv { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = (validationContext.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();

        if (ExpiryYear < today.Year || (ExpiryYear == today.Year && ExpiryMonth < today.Month))
        {
            yield return new ValidationResult("The card has expired.", [nameof(ExpiryMonth), nameof(ExpiryYear)]);
        }
    }
}
