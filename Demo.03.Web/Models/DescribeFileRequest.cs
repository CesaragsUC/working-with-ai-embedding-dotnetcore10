namespace Demo.Embedding.Web;

public sealed class DescribeFileRequest
{
    /// <example>
    /// Prompt para descrever a arquivo enviado.
    /// </example>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Envie um arquivo.
    /// </summary>
    public IFormFile File { get; set; } = default!;
}
