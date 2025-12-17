using Pgvector;

namespace Demo.Embedding.Web;

public class SobreClubes
{
    public Guid Id { get; set; }
    public Guid? ClubeId { get; set; }
    public string Name { get; set; }
    public string Country { get; set; }
    public string State { get; set; }
    public string Description { get; set; }
    public Vector Embedding { get; set; }
}
