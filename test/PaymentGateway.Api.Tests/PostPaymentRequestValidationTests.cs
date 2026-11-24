using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests;

public class PostPaymentRequestValidationTests
{
    [Fact]
    public void AcceptsAValidRequest()
    {
        Assert.Empty(InvalidFields(ValidRequest()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2222405343248")]
    [InlineData("22224053432488771234")]
    [InlineData("2222 4053 4324 8877")]
    [InlineData("222240534324887A")]
    public void RejectsAnInvalidCardNumber(string? cardNumber)
    {
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        Assert.Equal([nameof(PostPaymentRequest.CardNumber)], InvalidFields(request));
    }

    [Theory]
    [InlineData("22224053432488")]
    [InlineData("2222405343248877123")]
    public void AcceptsCardNumbersOf14To19Digits(string cardNumber)
    {
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        Assert.Empty(InvalidFields(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(13)]
    public void RejectsAnInvalidExpiryMonth(int? expiryMonth)
    {
        var request = ValidRequest();
        request.ExpiryMonth = expiryMonth;

        Assert.Equal([nameof(PostPaymentRequest.ExpiryMonth)], InvalidFields(request));
    }

    [Fact]
    public void RejectsAMissingExpiryYear()
    {
        var request = ValidRequest();
        request.ExpiryYear = null;

        Assert.Equal([nameof(PostPaymentRequest.ExpiryYear)], InvalidFields(request));
    }

    [Theory]
    [InlineData(11, 2026)]
    [InlineData(12, 2026)]
    [InlineData(1, 2027)]
    public void AcceptsACardThatExpiresThisMonthOrLater(int expiryMonth, int expiryYear)
    {
        var request = ValidRequest();
        request.ExpiryMonth = expiryMonth;
        request.ExpiryYear = expiryYear;

        Assert.Empty(InvalidFields(request));
    }

    [Theory]
    [InlineData(10, 2026)]
    [InlineData(12, 2025)]
    public void RejectsAnExpiredCard(int expiryMonth, int expiryYear)
    {
        var request = ValidRequest();
        request.ExpiryMonth = expiryMonth;
        request.ExpiryYear = expiryYear;

        Assert.Equal([nameof(PostPaymentRequest.ExpiryMonth), nameof(PostPaymentRequest.ExpiryYear)], InvalidFields(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gbp")]
    [InlineData("JPY")]
    [InlineData("GBPX")]
    public void RejectsAnUnsupportedCurrency(string? currency)
    {
        var request = ValidRequest();
        request.Currency = currency;

        Assert.Equal([nameof(PostPaymentRequest.Currency)], InvalidFields(request));
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void AcceptsTheSupportedCurrencies(string currency)
    {
        var request = ValidRequest();
        request.Currency = currency;

        Assert.Empty(InvalidFields(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsAnAmountThatIsNotPositive(int? amount)
    {
        var request = ValidRequest();
        request.Amount = amount;

        Assert.Equal([nameof(PostPaymentRequest.Amount)], InvalidFields(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12A")]
    public void RejectsAnInvalidCvv(string? cvv)
    {
        var request = ValidRequest();
        request.Cvv = cvv;

        Assert.Equal([nameof(PostPaymentRequest.Cvv)], InvalidFields(request));
    }

    [Theory]
    [InlineData("012")]
    [InlineData("1234")]
    public void AcceptsCvvsOf3Or4Digits(string cvv)
    {
        var request = ValidRequest();
        request.Cvv = cvv;

        Assert.Empty(InvalidFields(request));
    }

    private static PostPaymentRequest ValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    private static List<string> InvalidFields(PostPaymentRequest request)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection().AddSingleton<TimeProvider>(clock).BuildServiceProvider();

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request, services, null), results, validateAllProperties: true);

        return results.SelectMany(result => result.MemberNames).Distinct().ToList();
    }
}