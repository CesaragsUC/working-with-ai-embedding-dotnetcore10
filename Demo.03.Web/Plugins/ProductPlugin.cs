using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Demo.Embedding.Web;

public class ProductPlugin
{
    private readonly IServiceScopeFactory _scopeFactory;
    public ProductPlugin(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    //prompt: get the complete products list
    [KernelFunction("get_products")]
    [Description("Retrieves all products from the catalog.")]
    public async Task<List<Products>> GetAllProducts()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppEmbeddingDbContext>();
        return await context.Products.AsNoTracking().ToListAsync();
    }

    // prompt: get product with id 1 or get product with name Laptop
    [KernelFunction("get_product_by_id")]
    [Description("Retrieves a product by its ID.")]
    public async Task<Products?> GetProductById(int id)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppEmbeddingDbContext>();
        return context.Products.FirstOrDefault(p => p.Id == id);
    }

    // prompt: update the Laptop price to 200
    [KernelFunction("update_product")]
    [Description("Updates a product in the catalog.")]
    public async Task<List<Products>> UpdateProduct(int id , Products updatedProduct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppEmbeddingDbContext>();

        var product = context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null) return new List<Products>();


        product.Name = updatedProduct.Name;
        product.Price = updatedProduct.Price;
        product.Category = updatedProduct.Category;
        product.Description = updatedProduct.Description;

        context.Products.Remove(product);
        context.Products.Add(product);

        await context.SaveChangesAsync();

        return context.Products.ToList();
    }

}