namespace Demo.Embedding.Web.Configurations.ConfigsModel;

public sealed class AzureAIOptions
{
    public const string SectionName = "AzureAI";
    public Uri? Endpoint { get; set; }

    public string? ChatModel { get; set; }
    public string? ChatModelEndpoint { get; set; }

    public string? EmbeddingGeneratorModel { get; set; }
    public string? EmbeddingEndpoint { get; set; }

    public string? ImageModel { get; set; }
    public Uri? ImageModelEndpoint { get; set; }

    public string? AudioModel { get; set; }
    public Uri? AudioModelEndpoint { get; set; }

    public string? VideoModel { get; set; }
    public string? VideoModelEndpoint { get; set; }

}
