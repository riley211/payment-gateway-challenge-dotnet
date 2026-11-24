using Microsoft.OpenApi;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false)
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = RejectedPaymentResponse.Create);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Payment Gateway",
        Version = "v1",
        Description = """
            Process card payments through the acquiring bank and retrieve them later.

            **Test cards for the bank simulator.** The last digit decides the outcome:

            | Card number | Result |
            |---|---|
            | `2222405343248877` (odd) | 201 Authorized |
            | `2222405343248878` (even) | 201 Declined |
            | `2222405343248870` (zero) | 502, the bank is unavailable |

            Send an `Idempotency-Key` header to make retries safe: repeating it returns the original payment.
            """
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PaymentGateway.Api.xml"));
});
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
    .WithLogging(configureBuilder: null, configureOptions: logging => logging.IncludeFormattedMessage = true);

if (builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is not null)
{
    openTelemetry.UseOtlpExporter();
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PaymentMetrics>();
builder.Services.AddSingleton<IPaymentsRepository, PaymentsRepository>();
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