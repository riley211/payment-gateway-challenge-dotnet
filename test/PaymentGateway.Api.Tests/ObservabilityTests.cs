using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public sealed class ObservabilityTests : IDisposable
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

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task LogsTheOutcomeOfAPayment()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", Payment());
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        Assert.Contains(Logs(), log => log.Level == LogLevel.Information
            && log.Message.Contains(payment!.Id.ToString())
            && log.Message.Contains("card ending 8877")
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

    [Fact]
    public async Task CountsPaymentsByOutcome()
    {
        using var payments = new MetricCollector<long>(
            _factory.Services.GetRequiredService<IMeterFactory>(), PaymentMetrics.MeterName, PaymentMetrics.PaymentsInstrument);

        await _client.PostAsJsonAsync("/api/Payments", Payment());
        _bank.Response = new BankPaymentResponse(false, "");
        await _client.PostAsJsonAsync("/api/Payments", Payment());
        await _client.PostAsJsonAsync("/api/Payments", Payment() with { Currency = "JPY" });
        _bank.IsUnavailable = true;
        await _client.PostAsJsonAsync("/api/Payments", Payment());

        Assert.Equal(
            ["Authorized", "Declined", "Rejected", "BankUnavailable"],
            payments.GetMeasurementSnapshot().Select(measurement => measurement.Tags["outcome"]));
    }

    [Fact]
    public async Task CountsABankFailureOnceForConcurrentDuplicates()
    {
        using var payments = new MetricCollector<long>(
            _factory.Services.GetRequiredService<IMeterFactory>(), PaymentMetrics.MeterName, PaymentMetrics.PaymentsInstrument);
        _bank.IsUnavailable = true;
        _bank.Gate = new TaskCompletionSource();

        var duplicates = Enumerable.Range(0, 3).Select(_ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/Payments") { Content = JsonContent.Create(Payment()) };
            request.Headers.Add("Idempotency-Key", "order-123");
            return _client.SendAsync(request);
        }).ToList();
        await Task.Delay(200);
        _bank.Gate.SetResult();
        await Task.WhenAll(duplicates);

        Assert.Single(payments.GetMeasurementSnapshot());
        Assert.Single(Logs(), log => log.Exception is BankUnavailableException);
    }

    [Fact]
    public async Task ReportsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReturnsProblemDetailsForAnUnexpectedError()
    {
        _bank.UnexpectedError = new InvalidOperationException("Something went wrong in the gateway.");

        var response = await _client.PostAsJsonAsync("/api/Payments", Payment());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    private IReadOnlyList<FakeLogRecord> Logs() => _factory.Services.GetFakeLogCollector().GetSnapshot();

    private static PaymentRequest Payment() => new(CardNumber, 4, 2030, "GBP", 1050, Cvv);

    private sealed record PaymentRequest(string CardNumber, int ExpiryMonth, int ExpiryYear, string Currency, int Amount, string Cvv);
}