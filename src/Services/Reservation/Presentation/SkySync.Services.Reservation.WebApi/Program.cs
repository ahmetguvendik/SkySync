using System.Reflection;
using SkySync.Services.Reservation.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SkySync.Services.Reservation.Application.Features.Commands.Reservation.Requests.CreateReservationCommandRequest).Assembly));

// Add Persistence Services
builder.Services.AddPersistenceService(builder.Configuration);

// Add MassTransit with RabbitMQ and Saga State Machine
builder.Services.AddMassTransitService(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();