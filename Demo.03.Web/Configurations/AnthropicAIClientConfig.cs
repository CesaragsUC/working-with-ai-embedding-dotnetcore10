

using Anthropic.SDK;
using Anthropic.SDK.Examples;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Serilog;

namespace Demo.Embedding.Web;

public static class AnthropicAIClientConfig
{
    public static IServiceCollection AddAnthropicAIClientConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        Log.Information("Starting Anthropic AI Client Configuration...");

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

        var retryInterceptor = new RetryInterceptor(
            maxRetries: 3,
            initialDelay: TimeSpan.FromSeconds(1),
            backoffMultiplier: 2.0
        );

        IChatClient anthropicChatClient = new AnthropicClient(apiKeys: new APIAuthentication(apiKey)).Messages
            .AsBuilder()
            .UseFunctionInvocation()
            .UseKernelFunctionInvocation()
            .Build();

        services.AddChatClient(anthropicChatClient);

        services.AddSingleton<Kernel>(sp =>
        {
            var kernel = kernelBuilder.Build();

            // plugin criado via DI (não use AddFromType aqui)
            var productService = sp.GetRequiredService<IProductKf>();
            kernel.ImportPluginFromObject(productService, "Product");

            var textprocessorService = sp.GetRequiredService<ITextProcessorKf>();
            kernel.ImportPluginFromObject(textprocessorService, "TextProcessor");

            return kernel;
        });

        Log.Information("Anthropic AI Client Configuration completed.");

        return services;
    }
}
