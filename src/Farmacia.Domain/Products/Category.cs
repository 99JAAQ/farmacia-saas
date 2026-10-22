using Farmacia.Domain.Common;

namespace Farmacia.Domain.Products;

public class Category : TenantEntity
{
    private Category() { } // EF Core

    private Category(Guid pharmacyId) : base(pharmacyId) { }

    public string Name { get; private set; } = null!;

    public static Category Create(Guid pharmacyId, string name)
    {
        var category = new Category(pharmacyId);
        category.Rename(name);
        return category;
    }

    public void Rename(string name) => Name = Guard.Required(name, "El nombre de la categoría");
}
