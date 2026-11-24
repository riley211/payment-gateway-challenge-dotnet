using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly PaymentsService _paymentsService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentsRepository paymentsRepository,
        PaymentsService paymentsService,
        ILogger<PaymentsController> logger)
    {
        _paymentsRepository = paymentsRepository;
        _paymentsService = paymentsService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType<PostPaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        PostPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        try
        {
            var payment = await _paymentsService.ProcessPaymentAsync(request, idempotencyKey);

            return CreatedAtRoute("GetPayment", new { id = payment.Id }, payment);
        }
        catch (BankUnavailableException)
        {
            return Problem(
                title: "The acquiring bank is unavailable.",
                detail: "The payment could not be processed.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (IdempotencyKeyReusedException exception)
        {
            _logger.LogWarning("Idempotency-Key {IdempotencyKey} was reused for a different payment", idempotencyKey);

            return Problem(
                title: "The Idempotency-Key has already been used.",
                detail: exception.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    [HttpGet("{id:guid}", Name = "GetPayment")]
    [ProducesResponseType<PostPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostPaymentResponse?>> GetPaymentAsync(Guid id)
    {
        var payment = await _paymentsRepository.GetAsync(id);

        return payment is null ? NotFound() : Ok(payment);
    }
}