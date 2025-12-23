using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Demo.Embedding.Web.Models;

public sealed class ProductUpdatePatchDto
{
    [JsonPropertyName("name")]
    [Description("New name for the product (optional).")]
    public string? Name { get; init; }

    [JsonPropertyName("category")]
    [Description("New category for the product (optional).")]
    public string? Category { get; init; }

    [JsonPropertyName("price")]
    [Description("New price for the product (optional).")]
    public decimal? Price { get; init; }

    [JsonPropertyName("description")]
    [Description("New description for the product (optional).")]
    public string? Description { get; init; }
}
