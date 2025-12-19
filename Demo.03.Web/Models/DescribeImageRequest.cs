namespace Demo.Embedding.Web;

public sealed class DescribeImageRequest
{
    /// <example>
    /// Prompt para descrever a imagem.
    /// </example>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Envie uma Imagem.
    /// </summary>
    public IFormFile File { get; set; } = default!;
}
