using Demo.Embedding.Web.Models;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Serilog;
using System.ComponentModel;
using System.Text.Json;

namespace Demo.Embedding.Web;

public interface IProductKf
{
    [KernelFunction("get_products")]
    [Description("Retrieves all products from the catalog.")]
    Task<List<ProductDto>> GetAllProducts();

    [KernelFunction("get_product_by_id")]
    [Description("Gets a product by its ID.")]
    Task<ProductDto?> GetProductById(string id);

    [KernelFunction("update_product_price")]
    [Description("Updates a product in the catalog.")]
    Task<ProductDto> UpdateProductPrice(Guid id, decimal price);

    [KernelFunction("update_product_patch")]
    [Description("Updates a product Patch")]
    Task<ProductDto> UpdateProductPatch(Guid id, ProductUpdatePatchDto product);

    [KernelFunction("update_product")]
    [Description("Updates a product")]
    Task<ProductDto> UpdateProductPrice(
    Guid id,
    decimal price ,
    string name ,
    string category ,
    string description);

    [KernelFunction("get_product_by_price")]
    [Description("Retrieves a product by its price.")]
    Task<List<ProductDto>> GetProductByPrice(decimal price);

    [KernelFunction("get_product_by_description")]
    [Description("Retrieves a product by its description.")]
    Task<List<ProductDto>> GetProductByDescription(string description);

    [KernelFunction("get_product_by_budget")]
    [Description("Retrieves a product by its budget.")]
    Task<List<ProductDto>> GetProductByBudget(decimal budget);

    [KernelFunction("get_json_product_by_id")]
    [Description("Get product by ID and return as JSON.")]
    Task<string> GetProductByIdJson(
    [Description("Product ID")] string id);
}

public sealed class ProductKf: IProductKf
{
    private readonly IDbContextFactory<AppEmbeddingDbContext> _factory;

    public ProductKf(IDbContextFactory<AppEmbeddingDbContext> factory)
        => _factory = factory;

    //prompt: get the complete products list
    [KernelFunction("get_products")]
    [Description("Retrieves all products from the catalog.")]
    [return: Description("An array of products.")]
    public async Task<List<ProductDto>> GetAllProducts()
    {
        Log.Information($"[SK] Function get_all_products called.");
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
    [Description("Gets a product by its ID.")]
    public async Task<ProductDto?> GetProductById([Description("The product ID (UUID string).")] string id)
    {
        Log.Information($"[SK] Function get_product_by_id called.");

        if (!Guid.TryParse(id, out var guid))
        {
            Log.Error($"[SK] Falha ao converter GUID: {id}");
            return null;
        }

        await using var _context = await _factory.CreateDbContextAsync();
        return await _context.Products.AsNoTracking().Where(p => p.Id == guid).Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price
        }).FirstOrDefaultAsync();
    }

    [KernelFunction("get_json_product_by_id")]
    [Description("Gets a product by its ID.")]
    public async Task<string> GetProductByIdJson([Description("The product ID (UUID string).")] string id)
    {
        try
        {
            Log.Information($"[SK] Function get_json_product_by_id called.");

            if (!Guid.TryParse(id, out var guid))
            {
                return JsonSerializer.Serialize(new { error = "Invalid GUID format" });
            }

            await using var context = await _factory.CreateDbContextAsync();

            var product = await context.Products
                .AsNoTracking()
                .Where(p => p.Id == guid)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    price = p.Price,
                    category = p.Category,
                    description = p.Description
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return JsonSerializer.Serialize(new { error = "Product not found" });
            }

            //Retornar JSON string (mais fácil pra AI processar)
            return JsonSerializer.Serialize(product);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
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

        Log.Information($"[SK] Updating product {id} price -> {price}");
        product.Price = price;

        _context.Products.Update(product);
        var rows =  await _context.SaveChangesAsync();

        Log.Information($"[SK] SaveChanges rows: {rows}");
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
        Log.Information($"[SK] Function get_product_by_price called.");
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
        Log.Information($"[SK] Function get_product_by_description called.");

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

        Log.Information($"[SK] Function get_product_by_budget called.");

        await using var _context = await _factory.CreateDbContextAsync();
        return await _context.Products.Where(p => p.Price <= budget).Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price
        }).ToListAsync();
    }

    /// <summary>
    /// Opcao passando DTO para atualizar o produto. As vezes LLM pode ter mais dificuldade em popular o DTO corretamente
    /// Tentar passar um objeto json no prompt com os campos a serem atualizados
    /// </summary>
    /// <param name="id"></param>
    /// <param name="product"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    [KernelFunction("update_product_patch")]
    [Description("Updates a product. Only fields provided in the patch will be changed.")]
    public async Task<ProductDto> UpdateProductPatch(
        [Description("The product id to update.")] Guid id,
        [Description("Fields to update. Omit fields you don't want to change.")]
        ProductUpdatePatchDto patch)
    {
        Log.Information($"[SK] Function update_product_patch called.");

        if (patch is null)
            throw new ArgumentException("Patch cannot be null.");

        if (patch.Name is null && patch.Category is null && patch.Price is null && patch.Description is null)
            throw new ArgumentException("Patch must contain at least one field.");

        await using var ctx = await _factory.CreateDbContextAsync();

        var existing = await ctx.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (existing is null) throw new Exception("Product not found");

        // aplica SOMENTE o que veio
        if (patch.Name is not null) existing.Name = patch.Name;
        if (patch.Category is not null) existing.Category = patch.Category;
        if (patch.Price.HasValue) existing.Price = patch.Price.Value;
        if (patch.Description is not null) existing.Description = patch.Description;

        await ctx.SaveChangesAsync();

        return new ProductDto
        {
            Id = existing.Id,
            Name = existing.Name,
            Category = existing.Category,
            Price = existing.Price,
            Description = existing.Description
        };
    }

    /// <summary>
    /// Opcao com parametros isolados para LLM ter mais change de chamar essa function caso a opcao com DTO falhe
    /// </summary>
    /// <param name="id"></param>
    /// <param name="price"></param>
    /// <param name="name"></param>
    /// <param name="category"></param>
    /// <param name="description"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    [KernelFunction("update_product")]
    [Description("Updates a product")]
    public async Task<ProductDto> UpdateProductPrice(
        Guid id,
        decimal price,
        string name,
        string category,
        string description)
    {
        Log.Information($"[SK] Function update_product  called."); 

        await using var _context = await _factory.CreateDbContextAsync();

        var existingProduct = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);

        if (existingProduct == null) throw new Exception("Product not found");

        Log.Information($"[SK] Updating product {id} -> {existingProduct.Name}");

        existingProduct.Price = price;
        existingProduct.Name = name;
        existingProduct.Description = description;
        existingProduct.Category = category;

        _context.Products.Update(existingProduct);
        var rows = await _context.SaveChangesAsync();

        Log.Information($"[SK] SaveChanges rows: {rows}");
        return new ProductDto
        {
            Id = existingProduct.Id,
            Name = existingProduct.Name,
            Description = existingProduct.Description,
            Price = existingProduct.Price
        };
    }
}
