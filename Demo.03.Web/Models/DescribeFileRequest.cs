namespace Demo.Embedding.Web;

public sealed class DescribeFileRequest
{
    public string Prompt { get; set; } = string.Empty;
    public IFormFile File { get; set; } = default!;
}
