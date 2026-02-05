using System.Reflection;
using FluentValidation;
using Serilog;
using Steeltoe.Discovery.Eureka;
using SkySync.Services.Flight.Application.Behaviors;
using SkySync.Services.Flight.Application.Validators;
using SkySync.Services.Flight.Infrastructure.Cache;
using SkySync.Services.Flight.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Serilog: Configuration'dan oku, ServiceName ile zenginleştir, Console + Seq
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("ServiceName", "Flight")
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

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

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
        diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value);
        diagnosticContext.Set("StatusCode", httpContext.Response.StatusCode);
    };
});

app.UseAuthorization();

// Tutarlı hata response: message, code (opsiyonel), errors (validasyon)
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        if (exception is ValidationException validationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            var errors = validationException.Errors.Select(e => new { propertyName = e.PropertyName, errorMessage = e.ErrorMessage }).ToList();
            await context.Response.WriteAsJsonAsync(new { message = "Validasyon hatası.", errors });
        }
        else if (exception is KeyNotFoundException keyEx)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { message = keyEx.Message ?? "Kayıt bulunamadı.", code = "NOT_FOUND" });
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { message = "Bir hata oluştu. Lütfen tekrar deneyin.", code = "INTERNAL_ERROR" });
        }
    });
});

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();