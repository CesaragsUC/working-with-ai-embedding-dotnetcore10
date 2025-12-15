using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Demo.Embedding.Web;

public class ProductPlugin
{
    private readonly List<Product> _products = new()
    {
        new Product(1, "Laptop", 999.99m),
        new Product(2, "Smartphone", 499.99m),
        new Product(3, "Tablet", 299.99m)
    };

    //prompt: get the complete products list
    [KernelFunction("get_products")]
    [Description("Retrieves all products from the catalog.")]
    public async Task<List<Product>> GetAllProducts()
    {
        return _products;
    }

    // prompt: get product with id 1 or get product with name Laptop
    [KernelFunction("get_product_by_id")]
    [Description("Retrieves a product by its ID.")]
    public async Task<Product?> GetProductById(int id)
    {
        return _products.FirstOrDefault(p => p.Id == id);
    }

    // prompt: update the Laptop price to 200
    [KernelFunction("update_product")]
    [Description("Updates a product in the catalog.")]
    public async Task<List<Product>> UpdateProduct(int id ,Product updatedProduct)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product == null) return new List<Product>();


        var productDto = product with { Name = updatedProduct.Name, Price = updatedProduct.Price };

         _products.Remove(product);
         _products.Add(productDto);

        return _products;
    }

}
public record Product(int Id, string Name, decimal Price);
