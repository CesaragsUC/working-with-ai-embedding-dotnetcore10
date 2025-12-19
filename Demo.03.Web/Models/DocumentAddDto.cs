namespace Demo.Embedding.Web;

public class DocumentAddDto
{

    /// <example>
    /// Titulo do documento enviado.
    /// </example>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Envie o documento.
    /// </summary>
    public IFormFile Document { get; set; } = default!;
}