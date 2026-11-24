using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class ObservabilityTests
{
    private const string CardNumber = "2222405343248877";
    private const string Cvv = "4321";

    private readonly FakeAcquiringBank _bank = new();
    private readonly WebApplicationFactory<PaymentsController> _factory;
    private readonly HttpClient _client;

    public ObservabilityTests()
    {
        _factory = new WebApplicationFactory<PaymentsController>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services
                .AddLogging(logging => logging.AddFakeLogging())
                .AddSingleton<IAcquiringBank>(_bank)));
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task LogsTheOutcomeOfAPayment()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", Payment());
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        Assert.Contains(Logs(), log => log.Level == LogLevel.Information
            && log.Message.Contains(payment!.Id.ToString())
            && log.Message.Contains("Authorized"));
    }

    [Fact]
    public async Task LogsWhenTheBankIsUnavailable()
    {
        _bank.IsUnavailable = true;

        await _client.PostAsJsonAsync("/api/Payments", Payment());

        Assert.Contains(Logs(), log => log.Level == LogLevel.Warning && log.Exception is BankUnavailableException);
    }

    [Fact]
    public async Task NeverLogsTheCardNumberOrCvv()
    {
        await _client.PostAsJsonAsync("/api/Payments", Payment());
        _bank.IsUnavailable = true;
        await _client.PostAsJsonAsync("/api/Payments", Payment());
        await _client.PostAsJsonAsync("/api/Payments", Payment() with { Currency = "JPY" });

        Assert.All(Logs(), log =>
        {
            var everything = $"{log.Message} {log.Exception} {string.Join(" ", log.StructuredState?.Select(pair => pair.Value) ?? [])}";
            Assert.DoesNotContain(CardNumber, everything);
            Assert.DoesNotContain(Cvv, everything);
        });
    }

    private IReadOnlyList<FakeLogRecord> Logs() => _factory.Services.GetFakeLogCollector().GetSnapshot();

    private static PaymentRequest Payment() => new(CardNumber, 4, 2030, "GBP", 1050, Cvv);

    private record PaymentRequest(string CardNumber, int ExpiryMonth, int ExpiryYear, string Currency, int Amount, string Cvv);
}