namespace Demo.Embedding.Web.Configurations.ConfigsModel;

public sealed class OllamaAIOptions
{
    public const string SectionName = "OllamaAI";
    public string? ChatModel { get; set; }
    public string? EmbeddingGeneratorModel { get; set; }
    public string? ImageModel { get; set; }
    public string? AudioModel { get; set; }
    public string? VideoModel { get; set; }
    public string? ServerUrl { get; set; }

}
