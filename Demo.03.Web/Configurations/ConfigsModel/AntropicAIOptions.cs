namespace Demo.Embedding.Web.Configurations.ConfigsModel;

public sealed class AntropicAIOptions
{
    public const string SectionName = "AnthropicAI";
    public string? ChatModel { get; set; }
    public string? EmbeddingGeneratorModel { get; set; }
    public string? ImageModel { get; set; }
    public string? AudioModel { get; set; }
    public string? VideoModel { get; set; }

}