using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

public static class RejectedPaymentResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var services = context.HttpContext.RequestServices;
        var problem = services.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(context.HttpContext, context.ModelState);

        problem.Title = "The payment was rejected because some fields are invalid.";
        problem.Errors = problem.Errors.ToDictionary(error => FieldName(error.Key), error => error.Value);
        problem.Extensions["paymentStatus"] = PaymentStatus.Rejected;

        services.GetRequiredService<PaymentMetrics>().PaymentProcessed(PaymentStatus.Rejected);
        services.GetRequiredService<ILogger<PostPaymentRequest>>().LogInformation(
            "Payment rejected because of invalid fields: {InvalidFields}", string.Join(", ", problem.Errors.Keys));

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private static string FieldName(string modelStateKey) =>
        JsonNamingPolicy.CamelCase.ConvertName(modelStateKey.StartsWith("$.", StringComparison.Ordinal) ? modelStateKey[2..] : modelStateKey);
}