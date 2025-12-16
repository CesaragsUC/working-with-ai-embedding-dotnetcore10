using Pgvector;

namespace Demo.Embedding.Web;

public class MyDemoDocumentRecomendation
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string DocText { get; set; }
    public Guid DocumentId { get; set; }
    public Vector Embedding { get; set; }
}
