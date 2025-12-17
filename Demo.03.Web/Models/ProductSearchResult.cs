namespace Demo.Embedding.Web;

public class ProductSearchResult
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public decimal Price { get; set; }
    public string Description { get; set; } = default!;
}
