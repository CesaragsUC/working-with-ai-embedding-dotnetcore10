namespace Demo.Embedding.Web.Configurations.ConfigsModel;

public sealed class AzureAIOptions
{
    public const string SectionName = "AzureAI";
    public string? ChatModel { get; set; }
    public string? EmbeddingGeneratorModel { get; set; }
    public string? ImageModel { get; set; }
    public string? AudioModel { get; set; }
    public string? VideoModel { get; set; }
    public string? ServerUrl { get; set; }
    public string? ChatDeployment { get; set; }
    public string? Endpoint { get; set; }

}
