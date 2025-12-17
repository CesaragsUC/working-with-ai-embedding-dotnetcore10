using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Demo.Embedding.Web;

public class ProductPlugin
{
    private readonly IDbContextFactory<AppEmbeddingDbContext> _factory;

    public ProductPlugin(IDbContextFactory<AppEmbeddingDbContext> factory)
        => _factory = factory;

    //prompt: get the complete products list
    [KernelFunction("get_products")]
    [Description("Retrieves all products from the catalog.")]
    public async Task<List<Products>> GetAllProducts()
    {
        await using var _context = await _factory.CreateDbContextAsync();
        return await _context.Products.AsNoTracking().ToListAsync();
    }

    // prompt: get product with id 1 or get product with name Laptop
    [KernelFunction("get_product_by_id")]
    [Description("Retrieves a product by its ID.")]
    public async Task<Products?> GetProductById(Guid id)
    {
        await using var _context = await _factory.CreateDbContextAsync();
        return _context.Products.FirstOrDefault(p => p.Id == id);
    }

    // prompt: update the Laptop price to 200
    [KernelFunction("update_product_price")]
    [Description("Updates a product in the catalog.")]
    public async Task<Products> UpdateProductPrice(Guid id , decimal price)
    {
        await using var _context = await _factory.CreateDbContextAsync();

        var product = _context.Products.FirstOrDefault(p => p.Id == id);

        if (product == null)  throw new Exception("Product not found");

        Console.WriteLine($"[SK] Updating product {id} price -> {price}");
        product.Price = price;

        _context.Products.Update(product);

        var rows =  await _context.SaveChangesAsync();

        Console.WriteLine($"[SK] SaveChanges rows: {rows}");
        return product;
    }

}