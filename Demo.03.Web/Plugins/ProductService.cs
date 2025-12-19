using Demo.Embedding.Web.Models;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Demo.Embedding.Web;

public interface IProductService
{
    [KernelFunction("get_products")]
    [Description("Retrieves all products from the catalog.")]
    Task<List<ProductDto>> GetAllProducts();

    [KernelFunction("get_product_by_id")]
    [Description("Retrieves a product by its ID.")]
    Task<ProductDto?> GetProductById(Guid id);

    [KernelFunction("update_product_price")]
    [Description("Updates a product in the catalog.")]
    Task<ProductDto> UpdateProductPrice(Guid id, decimal price);

    [KernelFunction("get_product_by_price")]
    [Description("Retrieves a product by its price.")]
    Task<List<ProductDto>> GetProductByPrice(decimal price);

    [KernelFunction("get_product_by_description")]
    [Description("Retrieves a product by its description.")]
    Task<List<ProductDto>> GetProductByDescription(string description);

    [KernelFunction("get_product_by_budget")]
    [Description("Retrieves a product by its budget.")]
    Task<List<ProductDto>> GetProductByBudget(decimal budget);
}

public sealed class ProductService: IProductService
{
    private readonly IDbContextFactory<AppEmbeddingDbContext> _factory;

    public ProductService(IDbContextFactory<AppEmbeddingDbContext> factory)
        => _factory = factory;

    //prompt: get the complete products list
    [KernelFunction("get_products")]
    [Description("Retrieves all products from the catalog.")]
    [return: Description("An array of products.")]
    public async Task<List<ProductDto>> GetAllProducts()
    {
        await using var _context = await _factory.CreateDbContextAsync();
        return await _context.Products.AsNoTracking().Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price
        }).ToListAsync();
    }

    // prompt: get product with id 1 or get product with name Laptop
    [KernelFunction("get_product_by_id")]
    [Description("Retrieves a product by its ID.")]
    [return: Description("Return a product based on its ID.")]
    public async Task<ProductDto?> GetProductById([Description("product Id.")] Guid id)
    {
        await using var _context = await _factory.CreateDbContextAsync();
        return await _context.Products.AsNoTracking().Where(p => p.Id == id).Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price
        }).FirstOrDefaultAsync();
    }

    // prompt: update the Laptop price to 200
    [KernelFunction("update_product_price")]
    [Description("Updates a product in the catalog.")]
    [return: Description("Update a product price based on its ID.")]
    public async Task<ProductDto> UpdateProductPrice(
        [Description("product Id.")] Guid id ,
        [Description("product Price.")] decimal price)
    {
        await using var _context = await _factory.CreateDbContextAsync();

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)  throw new Exception("Product not found");

        Console.WriteLine($"[SK] Updating product {id} price -> {price}");
        product.Price = price;

        var rows =  await _context.SaveChangesAsync();

        Console.WriteLine($"[SK] SaveChanges rows: {rows}");
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price
        };
    }

    [KernelFunction("get_product_by_price")]
    [Description("Retrieves a product by its price.")]
    [return: Description("return a list of products based on its price.")]
    public async Task<List<ProductDto>> GetProductByPrice([Description("product Price.")] decimal price)
    {
        await using var _context = await _factory.CreateDbContextAsync();
        return await _context.Products.Where(p => p.Price == price).Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price
        }).ToListAsync();
    }

    [KernelFunction("get_product_by_description")]
    [Description("Retrieves a product by its description.")]
    [return: Description("return a list of products based on its description.")]
    public async Task<List<ProductDto>> GetProductByDescription([Description("product Description.")] string description)
    {
        await using var _context = await _factory.CreateDbContextAsync();
        return await _context.Products.Where(p => p.Description.Contains(description)).Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price
        }).ToListAsync();
    }

    [KernelFunction("get_product_by_budget")]
    [Description("Retrieves a product by its budget.")]
    [return: Description("return a list of products based on its budget.")]
    public async Task<List<ProductDto>> GetProductByBudget([Description("product Budget.")] decimal budget)
    {
        await using var _context = await _factory.CreateDbContextAsync();
        return await _context.Products.Where(p => p.Price <= budget).Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price
        }).ToListAsync();
    }
}