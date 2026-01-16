namespace Demo.Embedding.Web.Configurations.ConfigsModel;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";
    public Uri? Endpoint { get; set; }

    public string? ChatModel { get; set; }
    public Uri? ChatModelEndpoint { get; set; }

    public string? EmbeddingGeneratorModel { get; set; }
    public Uri? EmbeddingEndpoint { get; set; }

    public string? ImageModel { get; set; }
    public Uri? ImageModelEndpoint { get; set; }

    public string? AudioModel { get; set; }
    public Uri? AudioModelEndpoint { get; set; }

    public string? VideoModel { get; set; }
    public string? VideoModelEndpoint { get; set; }

}
