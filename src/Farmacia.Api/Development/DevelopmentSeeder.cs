using Farmacia.Domain.Common;
using Farmacia.Domain.Pharmacies;
using Farmacia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Api.Development;

internal static class DevelopmentSeeder
{
    /// <summary>Makes sure the development pharmacy exists (found by NIT) and points the current pharmacy at it.</summary>
    public static async Task SeedAsync(WebApplication app)
    {
        var name = app.Configuration["Development:PharmacyName"] ?? "Droguería de Desarrollo";
        var nit = Nit.Parse(app.Configuration["Development:PharmacyNit"]);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();

        var pharmacy = await db.Pharmacies.SingleOrDefaultAsync(p => p.TaxId == nit);
        if (pharmacy is null)
        {
            pharmacy = Pharmacy.Create(name, nit.Value);
            db.Pharmacies.Add(pharmacy);
            await db.SaveChangesAsync();
        }

        app.Services.GetRequiredService<DevelopmentCurrentPharmacy>().Initialize(pharmacy.Id);
    }
}
