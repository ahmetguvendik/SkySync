using System.Reflection;
using FluentValidation;
using Steeltoe.Discovery.Eureka;
using SkySync.Services.Flight.Application.Behaviors;
using SkySync.Services.Flight.Application.Validators;
using SkySync.Services.Flight.Infrastructure.Cache;
using SkySync.Services.Flight.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SkySync.Services.Flight.Application.Features.Commands.Flight.Requests.CreateFlightCommandRequest).Assembly));

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateFlightCommandRequestValidator>();
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Add Persistence Services
builder.Services.AddPersistenceService(builder.Configuration);

// Add MassTransit with Consumers
builder.Services.AddMassTransitService(builder.Configuration);

// Add Cache Service (Redis)
builder.Services.AddCacheService(builder.Configuration);

// Eureka Service Discovery - Register with Eureka
builder.Services.AddEurekaDiscoveryClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

// ValidationException -> 400 Bad Request; diğer hatalar 500
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        if (exception is ValidationException validationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            var errors = validationException.Errors.Select(e => new { e.PropertyName, e.ErrorMessage });
            await context.Response.WriteAsJsonAsync(new { errors });
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { message = "An error occurred." });
        }
    });
});

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();