using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class IdempotencyTests
{
    private readonly FakeAcquiringBank _bank = new();
    private readonly PaymentsRepository _repository = new();
    private readonly HttpClient _client;

    public IdempotencyTests()
    {
        _client = new WebApplicationFactory<PaymentsController>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.AddSingleton(_repository);
                services.AddSingleton<IAcquiringBank>(_bank);
            }))
            .CreateClient();
    }

    [Fact]
    public async Task ReturnsTheOriginalPaymentWhenAKeyIsRepeated()
    {
        var first = await PostAsync(Payment(amount: 1050), "order-123");
        var repeat = await PostAsync(Payment(amount: 1050), "order-123");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, repeat.StatusCode);
        Assert.Equal((await IdOf(first)), (await IdOf(repeat)));
        Assert.Single(_bank.Requests);
        Assert.Single(_repository.Payments);
    }

    [Fact]
    public async Task CallsTheBankOnceForConcurrentDuplicates()
    {
        _bank.Gate = new TaskCompletionSource();

        var duplicates = Enumerable.Range(0, 5)
            .Select(_ => PostAsync(Payment(amount: 1050), "order-123"))
            .ToList();
        await Task.Delay(200);
        _bank.Gate.SetResult();
        var responses = await Task.WhenAll(duplicates);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var ids = await Task.WhenAll(responses.Select(IdOf));
        Assert.Single(ids.Distinct());
        Assert.Single(_bank.Requests);
    }

    [Fact]
    public async Task RejectsAKeyReusedForADifferentPayment()
    {
        await PostAsync(Payment(amount: 1050), "order-123");
        var reused = await PostAsync(Payment(amount: 9999), "order-123");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        Assert.Equal("application/problem+json", reused.Content.Headers.ContentType!.MediaType);
        Assert.Single(_bank.Requests);
        Assert.Single(_repository.Payments);
    }

    [Fact]
    public async Task AllowsARetryAfterTheBankFails()
    {
        _bank.IsUnavailable = true;
        var failed = await PostAsync(Payment(amount: 1050), "order-123");

        _bank.IsUnavailable = false;
        var retried = await PostAsync(Payment(amount: 1050), "order-123");

        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        Assert.Equal(2, _bank.Requests.Count);
        Assert.Single(_repository.Payments);
    }

    [Fact]
    public async Task ProcessesEachKeySeparately()
    {
        var first = await PostAsync(Payment(amount: 1050), "order-123");
        var second = await PostAsync(Payment(amount: 1050), "order-456");

        Assert.NotEqual(await IdOf(first), await IdOf(second));
        Assert.Equal(2, _bank.Requests.Count);
    }

    [Fact]
    public async Task ProcessesEveryRequestWithoutAKey()
    {
        var first = await PostAsync(Payment(amount: 1050), idempotencyKey: null);
        var second = await PostAsync(Payment(amount: 1050), idempotencyKey: null);

        Assert.NotEqual(await IdOf(first), await IdOf(second));
        Assert.Equal(2, _bank.Requests.Count);
    }

    private Task<HttpResponseMessage> PostAsync(object payment, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Payments") { Content = JsonContent.Create(payment) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return _client.SendAsync(request);
    }

    private static async Task<Guid> IdOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<PostPaymentResponse>())!.Id;

    private static object Payment(int amount) => new
    {
        cardNumber = "2222405343248877",
        expiryMonth = 4,
        expiryYear = 2030,
        currency = "GBP",
        amount,
        cvv = "123"
    };
}