using System.Collections.Concurrent;

using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class FakeAcquiringBank : IAcquiringBank
{
    public BankPaymentResponse Response { get; set; } = new(true, "0bb07405-6d44-4b50-a14f-7ae0beff13ad");
    public bool IsUnavailable { get; set; }
    public Exception? UnexpectedError { get; set; }
    public TaskCompletionSource? Gate { get; set; }
    public ConcurrentQueue<BankPaymentRequest> Requests { get; } = new();

    public async Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request)
    {
        Requests.Enqueue(request);

        if (Gate is not null)
        {
            await Gate.Task;
        }

        if (UnexpectedError is not null)
        {
            throw UnexpectedError;
        }

        return IsUnavailable
            ? throw new BankUnavailableException("The bank responded with 503.")
            : Response;
    }
}