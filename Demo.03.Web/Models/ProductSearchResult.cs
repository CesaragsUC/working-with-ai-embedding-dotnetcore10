namespace Demo.Embedding.Web;

public class ProductSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public decimal Price { get; set; }
    public string Description { get; set; } = default!;
}
