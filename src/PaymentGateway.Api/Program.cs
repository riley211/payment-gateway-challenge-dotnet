using Microsoft.AspNetCore.Mvc;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        var validationProblem = options.InvalidModelStateResponseFactory;
        options.InvalidModelStateResponseFactory = context =>
        {
            var services = context.HttpContext.RequestServices;
            services.GetRequiredService<PaymentMetrics>().PaymentProcessed(PaymentStatus.Rejected);
            services.GetRequiredService<ILogger<PostPaymentRequest>>()
                .LogInformation("Payment rejected because of invalid fields: {InvalidFields}", string.Join(", ", context.ModelState.Keys));

            var result = (ObjectResult)validationProblem(context);
            ((ProblemDetails)result.Value!).Extensions["paymentStatus"] = PaymentStatus.Rejected;
            return result;
        };
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var openTelemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("payment-gateway"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options => options.Filter = context => context.Request.Path != "/health")
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(PaymentMetrics.MeterName))
    .WithLogging();

if (builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is not null)
{
    openTelemetry.UseOtlpExporter();
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PaymentMetrics>();
builder.Services.AddSingleton<PaymentsRepository>();
builder.Services.AddSingleton<IdempotencyStore>();
builder.Services.AddScoped<PaymentsService>();
builder.Services.AddHttpClient<IAcquiringBank, BankSimulatorClient>((services, client) =>
{
    var bankSimulator = services.GetRequiredService<IConfiguration>().GetSection("BankSimulator");
    client.BaseAddress = new Uri(bankSimulator["BaseUrl"]!);
    client.Timeout = bankSimulator.GetValue<TimeSpan>("Timeout");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();