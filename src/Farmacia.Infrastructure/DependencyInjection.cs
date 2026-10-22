using Farmacia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Farmacia")
            ?? throw new InvalidOperationException(
                "Connection string 'Farmacia' is missing. Set it with: dotnet user-secrets set \"ConnectionStrings:Farmacia\" \"...\" --project src/Farmacia.Api");

        services.AddDbContext<FarmaciaDbContext>(options => options.UseSqlServer(connectionString));

        return services;
    }
}
