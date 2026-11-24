namespace PaymentGateway.Api.Services;

public class IdempotencyKeyReusedException()
    : Exception("The Idempotency-Key has already been used for a different payment.");
