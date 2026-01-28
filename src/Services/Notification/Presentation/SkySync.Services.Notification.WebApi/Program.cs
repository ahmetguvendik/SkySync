using SkySync.Services.Notification.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add Notification Services (DI + MassTransit)
builder.Services.AddNotificationServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
