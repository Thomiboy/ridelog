var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCqrs();
builder.Services.AddRideLogPersistence(
    builder.Configuration.GetConnectionString("RideLog")
        ?? throw new InvalidOperationException("Connection string 'RideLog' is missing."));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck");

app.Run();
