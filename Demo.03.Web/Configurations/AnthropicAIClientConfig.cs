
using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;

namespace Demo.Embedding.Web;

public static class AnthropicAIClientConfig
{
    public static IServiceCollection AddAnthropicAIClientConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var kernelBuilder = Kernel.CreateBuilder();

        // ANTHROPIC_API_KEY vem das variaveis de ambiente do sistema Recomendado para evitar vazar em codigo fonte.
        // Em producao salvar em Azure Key Vault ou similar.
        var apiKey = configuration["ANTHROPIC_API_KEY"] ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not configured");
        var chatModel = configuration.GetSection("AnthropicAI:ChatModel").Value ?? "claude-haiku-4-5-20251001";


        // GOOGLE_API_KEY vem das variaveis de ambiente do sistema Recomendado para evitar vazar em codigo fonte.
        // Em producao salvar em Azure Key Vault ou similar.
        var geminiApiKey = configuration["GOOGLE_API_KEY"] ?? throw new InvalidOperationException("GOOGLE_API_KEY not configured");
        var geminiHttpClient = GeminiHttpClientHelper.CreateGeminiHttpClient(ignoreSslErrors: environment.IsDevelopment());
        var geminiEmbeddingModel = configuration.GetSection("GeminiAI:EmbeddingGeneratorModel").Value ?? "text-embedding-004";

        // Anthropic não tem Embedding, então usamos Gemini/OpenAI/AzureAi, para Embedding e Anthropic para Chat Completion
        kernelBuilder.AddGoogleAIEmbeddingGenerator(
            geminiEmbeddingModel!,
            geminiApiKey!,
            apiVersion: GoogleAIVersion.V1,
            serviceId: "GeminiEmbedding",
            geminiHttpClient);

        ///Chat Completion
        var anthropicClient = new AnthropicClient(new()
        {
            APIKey = apiKey
        })
                         .AsIChatClient(chatModel)
                         .AsBuilder()
                         .UseFunctionInvocation()
                         .Build();

        services.AddChatClient(anthropicClient);


        services.AddSingleton<Kernel>(sp =>
        {
            var kernel = kernelBuilder.Build();

            // plugin criado via DI (não use AddFromType aqui)
            var productService = sp.GetRequiredService<IProductService>();
            kernel.Plugins.AddFromObject(productService);

            return kernel;
        });

        return services;
    }
}
