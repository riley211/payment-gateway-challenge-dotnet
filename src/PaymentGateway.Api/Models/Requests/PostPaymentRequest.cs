using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Models.Requests;

public class PostPaymentRequest : IValidatableObject
{
    /// <example>2222405343248877</example>
    [Required(ErrorMessage = "Card number is required.")]
    [StringLength(19, MinimumLength = 14, ErrorMessage = "Card number must be between 14 and 19 digits.")]
    [RegularExpression("^[0-9]+$", ErrorMessage = "Card number must contain only digits.")]
    public string? CardNumber { get; set; }

    /// <example>4</example>
    [Required(ErrorMessage = "Expiry month is required.")]
    [Range(1, 12, ErrorMessage = "Expiry month must be between 1 and 12.")]
    public int? ExpiryMonth { get; set; }

    /// <example>2030</example>
    [Required(ErrorMessage = "Expiry year is required.")]
    public int? ExpiryYear { get; set; }

    /// <example>GBP</example>
    [Required(ErrorMessage = "Currency is required.")]
    [AllowedValues("GBP", "USD", "EUR", ErrorMessage = "Currency must be one of GBP, USD or EUR.")]
    public string? Currency { get; set; }

    /// <example>1050</example>
    [Required(ErrorMessage = "Amount is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Amount must be at least 1, in the currency's minor unit (1050 is 10.50).")]
    public int? Amount { get; set; }

    /// <example>123</example>
    [Required(ErrorMessage = "CVV is required.")]
    [StringLength(4, MinimumLength = 3, ErrorMessage = "CVV must be 3 or 4 digits.")]
    [RegularExpression("^[0-9]+$", ErrorMessage = "CVV must contain only digits.")]
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