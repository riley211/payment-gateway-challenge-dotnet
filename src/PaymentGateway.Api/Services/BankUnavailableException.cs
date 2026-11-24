namespace PaymentGateway.Api.Services;

public class BankUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
