using Microsoft.OpenApi.Models;
using SkySync.Services.Payment.Persistence;


var builder = WebApplication.CreateBuilder(args);

// Add Persistence & DB
builder.Services.AddPersistenceServices(builder.Configuration);

// Add MassTransit with Consumers
builder.Services.AddMassTransitService(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SkySync Payment API", Version = "v1" });
});
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SkySync Payment API v1"));
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
