using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkySync.Services.Flight.Application.Interfaces;
using SkySync.Services.Flight.Application.UnitOfWorks;
using SkySync.Services.Flight.Persistence.Contexts;
using SkySync.Services.Flight.Persistence.Repositories;
using SkySync.Services.Flight.Persistence.UnitOfWorks;

namespace SkySync.Services.Flight.Persistence;

public static class ServiceRegistration
{
    public static void AddPersistenceService(this IServiceCollection collection, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        collection.AddDbContext<FlightServiceDbContext>(opt =>
            opt.UseNpgsql(connectionString));
        
        //Repositories
        collection.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        collection.AddScoped<IOutboxRepository, OutboxRepository>();
        collection.AddScoped<IUnitOfWork, UnitOfWork>();
        
    }
}