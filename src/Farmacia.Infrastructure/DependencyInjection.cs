using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Authentication;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Infrastructure.Identity;
using Farmacia.Infrastructure.Persistence;
using Farmacia.Infrastructure.Persistence.Repositories;
using Farmacia.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Farmacia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Farmacia")
            ?? throw new InvalidOperationException(
                "Connection string 'Farmacia' is missing. Set it with: dotnet user-secrets set \"ConnectionStrings:Farmacia\" \"...\" --project src/Farmacia.Api");

        services.AddDbContext<FarmaciaDbContext>(options => options.UseSqlServer(connectionString));

        // Scoped, like the DbContext: one unit of work per request.
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<IPharmacyRepository, PharmacyRepository>();
        services.AddScoped<ICashSessionRepository, CashSessionRepository>();
        services.AddScoped<ICashSalesReader, CashSalesReader>();
        services.AddScoped<IUserReader, UserReader>();

        // Identity core only (users, roles, password hashing, lockout): no cookies, the API issues its own JWT.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // Length over complexity: cashiers type it many times a day.
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<FarmaciaDbContext>();

        // Fails at startup, not at the first login, if the signing key is missing or too short.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.IsValid(),
                $"Invalid '{JwtOptions.SectionName}' settings: Issuer, Audience and a base64 SigningKey of at least " +
                $"{JwtOptions.MinimumKeyBytes} bytes are required (set the key with dotnet user-secrets).")
            .ValidateOnStart();

        // TryAdd: tests replace it with a fake clock.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, ColombiaClock>();
        services.AddSingleton<AccessTokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
