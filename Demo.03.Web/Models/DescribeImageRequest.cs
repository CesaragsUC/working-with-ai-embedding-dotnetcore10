namespace Demo.Embedding.Web;

public sealed class DescribeImageRequest
{
    public string Prompt { get; set; } = string.Empty;
    public IFormFile File { get; set; } = default!;
}
