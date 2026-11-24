using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        var validationProblem = options.InvalidModelStateResponseFactory;
        options.InvalidModelStateResponseFactory = context =>
        {
            var result = (ObjectResult)validationProblem(context);
            ((ProblemDetails)result.Value!).Extensions["paymentStatus"] = PaymentStatus.Rejected;
            return result;
        };
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(TimeProvider.System);
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();