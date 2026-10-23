using ApexPost.Domain.Entities;

namespace ApexPost.Web.Models;

public sealed class ProductCataloguePageModel
{
    public string? Query { get; set; }
    public IReadOnlyList<Product> Products { get; init; } = [];
    public CreateProductInputModel NewProduct { get; init; } = new();
}
