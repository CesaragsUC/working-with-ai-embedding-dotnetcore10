

using Anthropic.SDK;
using Demo.Embedding.Web.Configurations.ConfigsModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Serilog;

namespace Demo.Embedding.Web;

/// <summary>
/// SDK Docs: https://github.com/tghamm/Anthropic.SDK
/// </summary>
public static class AnthropicAIClientConfig
{
    public static IServiceCollection AddAnthropicAIClientConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        Log.Information("Starting Anthropic AI Client Configuration...");

        // ANTHROPIC_API_KEY vem das variaveis de ambiente do sistema Recomendado para evitar vazar em codigo fonte.
        // Em producao salvar em Azure Key Vault ou similar.
        var apiKey = configuration["ANTHROPIC_API_KEY"] ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not configured");
        var chatModel = configuration.GetSection("AnthropicAI:ChatModel").Value ?? "claude-haiku-4-5-20251001";


        // GOOGLE_API_KEY vem das variaveis de ambiente do sistema Recomendado para evitar vazar em codigo fonte.
        // Em producao salvar em Azure Key Vault ou similar.
        var geminiApiKey = configuration["GOOGLE_API_KEY"] ?? throw new InvalidOperationException("GOOGLE_API_KEY not configured");

        services
            .AddOptions<AntropicAIOptions>()
            .Bind(configuration.GetSection(AntropicAIOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

      

        services.AddSingleton<IChatClient>(sp => {

            var options = sp.GetRequiredService<IOptions<AntropicAIOptions>>().Value;

            var anthropicChatClient = new AnthropicClient(apiKeys: new APIAuthentication(apiKey)).Messages
             .AsBuilder()
             .UseFunctionInvocation()
             .UseKernelFunctionInvocation()
             .Build();

            return anthropicChatClient;
        });

        services.AddSingleton<Kernel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GeminiAIOptions>>().Value;

            var kernelBuilder = Kernel.CreateBuilder();

            // cria httpClient custom aqui (ou use IHttpClientFactory)
            var geminiHttpClient = GeminiHttpClientHelper.CreateGeminiHttpClient(
               ignoreSslErrors: environment.IsDevelopment());

            // Anthropic não tem Embedding, então usamos Gemini/OpenAI/AzureAi, para Embedding e Anthropic para Chat Completion
            kernelBuilder.AddGoogleAIEmbeddingGenerator(options.EmbeddingGeneratorModel!, geminiApiKey!, serviceId: "GoogleAIEmbedding", httpClient: geminiHttpClient);

            var kernel = kernelBuilder.Build();

            kernel.ImportPluginFromObject(sp.GetRequiredService<IProductKf>(), "Product");
            kernel.ImportPluginFromObject(sp.GetRequiredService<ITextProcessorKf>(), "TextProcessor");

            return kernel;
        });

        Log.Information("Anthropic AI Client Configuration completed.");

        return services;
    }
}
