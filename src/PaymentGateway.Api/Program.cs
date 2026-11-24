using PaymentGateway.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PaymentsRepository>();
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
