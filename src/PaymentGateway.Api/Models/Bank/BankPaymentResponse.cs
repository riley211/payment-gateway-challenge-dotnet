namespace PaymentGateway.Api.Models.Bank;

public record BankPaymentResponse(bool Authorized, string AuthorizationCode);