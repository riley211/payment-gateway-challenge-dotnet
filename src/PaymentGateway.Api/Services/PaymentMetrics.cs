using System.Diagnostics.Metrics;

using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Services;

public class PaymentMetrics(IMeterFactory meterFactory)
{
    public const string MeterName = "PaymentGateway";
    public const string PaymentsInstrument = "payment_gateway.payments";

    private readonly Counter<long> _payments = meterFactory.Create(MeterName)
        .CreateCounter<long>(PaymentsInstrument, unit: "{payment}", description: "Payment requests by outcome.");

    public void PaymentProcessed(PaymentStatus status) => Record(status.ToString());

    public void BankUnavailable() => Record("BankUnavailable");

    private void Record(string outcome) => _payments.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
}