using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private readonly Random _random = new();

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var payment = new Payment(
            Id: Guid.NewGuid(),
            Status: PaymentStatus.Authorized,
            CardNumberLastFour: "0123",
            ExpiryMonth: _random.Next(1, 13),
            ExpiryYear: _random.Next(2027, 2031),
            Currency: "GBP",
            Amount: _random.Next(1, 10000),
            AuthorizationCode: "0bb07405-6d44-4b50-a14f-7ae0beff13ad",
            CreatedAt: DateTimeOffset.UtcNow);

        var paymentsRepository = new PaymentsRepository();
        await paymentsRepository.AddAsync(payment);

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => ((ServiceCollection)services)
                .AddSingleton<IPaymentsRepository>(paymentsRepository)))
            .CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal("0123", paymentResponse?.CardNumberLastFour);
    }

    [Fact]
    public async Task ReturnsTheStatusAsText()
    {
        // Arrange
        var payment = new Payment(
            Id: Guid.NewGuid(),
            Status: PaymentStatus.Declined,
            CardNumberLastFour: "8878",
            ExpiryMonth: 12,
            ExpiryYear: 2030,
            Currency: "GBP",
            Amount: 1050,
            AuthorizationCode: null,
            CreatedAt: DateTimeOffset.UtcNow);

        var paymentsRepository = new PaymentsRepository();
        await paymentsRepository.AddAsync(payment);

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => ((ServiceCollection)services)
                .AddSingleton<IPaymentsRepository>(paymentsRepository)))
            .CreateClient();

        // Act
        var json = await client.GetStringAsync($"/api/Payments/{payment.Id}");

        // Assert
        Assert.Contains("\"status\":\"Declined\"", json);
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}