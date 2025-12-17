namespace Demo.Embedding.Web;

public sealed record ClubeAddDto
{
    public string Name { get; set; }
    public string Country { get; set; }
    public string State { get; set; }
    public string Description { get; set; }
}