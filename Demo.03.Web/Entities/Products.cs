using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Demo.Embedding.Web;

public class Products
{
    [JsonPropertyName("id")]
    [Description("The unique identifier of the product.")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    [Description("The name of the product.")]
    public string Name { get; set; }

    [JsonPropertyName("category")]
    [Description("The category of the product.")]
    public string Category { get; set; }

    [JsonPropertyName("price")]
    [Description("The price of the product.")]
    public decimal Price { get; set; }

    [JsonPropertyName("description")]
    [Description("The description of the product.")]
    public string Description { get; set; }
}
