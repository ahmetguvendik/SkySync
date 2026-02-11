using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Application.UnitOfWorks;
using SkySync.Services.Identity.Persistence.Contexts;
using SkySync.Services.Identity.Persistence.Repositories;
using SkySync.Services.Identity.Persistence.Services;
using SkySync.Services.Identity.Persistence.UnitOfWorks;

namespace SkySync.Services.Identity.Persistence;

public static class ServiceRegistration
{
    public static void AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Worker için IdentityConnection, Identity WebApi için DefaultConnection kullan
        var connectionString = configuration.GetConnectionString("IdentityConnection")
            ?? configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<IdentityServiceDbContext>(opt => opt.UseNpgsql(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
    }
}
