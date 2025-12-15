using Pgvector;

namespace Demo.Embedding.Web;

public class SobreClubes
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Vector Embedding { get; set; }
}
