using System.Text.Json;

using PaymentGateway.Api.Models.Bank;

namespace PaymentGateway.Api.Services;

public class BankSimulatorClient(HttpClient httpClient) : IAcquiringBank
{
    private static readonly JsonSerializerOptions SnakeCase = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("payments", request, SnakeCase);

            if (!response.IsSuccessStatusCode)
            {
                throw new BankUnavailableException($"The bank responded with {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync<BankPaymentResponse>(SnakeCase)
                ?? throw new BankUnavailableException("The bank returned an empty response.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new BankUnavailableException("The bank could not be reached or returned an invalid response.", exception);
        }
    }
}