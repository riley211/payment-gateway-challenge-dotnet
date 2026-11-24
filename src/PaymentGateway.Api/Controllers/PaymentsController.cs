using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly PaymentsRepository _paymentsRepository;
    private readonly PaymentsService _paymentsService;

    public PaymentsController(PaymentsRepository paymentsRepository, PaymentsService paymentsService)
    {
        _paymentsRepository = paymentsRepository;
        _paymentsService = paymentsService;
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(PostPaymentRequest request)
    {
        try
        {
            var payment = await _paymentsService.ProcessPaymentAsync(request);

            return CreatedAtRoute("GetPayment", new { id = payment.Id }, payment);
        }
        catch (BankUnavailableException)
        {
            return Problem(
                title: "The acquiring bank is unavailable.",
                detail: "The payment could not be processed.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpGet("{id:guid}", Name = "GetPayment")]
    public async Task<ActionResult<PostPaymentResponse?>> GetPaymentAsync(Guid id)
    {
        var payment = _paymentsRepository.Get(id);

        return payment is null ? NotFound() : Ok(payment);
    }
}