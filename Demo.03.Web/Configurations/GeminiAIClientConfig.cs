using Demo.Embedding.Web.Configurations.ConfigsModel;
using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Serilog;

namespace Demo.Embedding.Web;

/// <summary>
/// // Doc Gemini Client SDK: https://googleapis.github.io/dotnet-genai/
/// </summary>
public static class GeminiAIClientConfig
{
    public static IServiceCollection AddGeminiAIClientConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        Log.Information("Starting Gemini AI Client Configuration...");

        // GOOGLE_API_KEY vem das variaveis de ambiente do sistema Recomendado para evitar vazar em codigo fonte.
        // Em producao salvar em Azure Key Vault ou similar.
        var apiKey = configuration["GOOGLE_API_KEY"]
            ?? throw new InvalidOperationException("GOOGLE_API_KEY not configured");

        services
        .AddOptions<GeminiAIOptions>()
        .Bind(configuration.GetSection(GeminiAIOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

        //Regista Alternativa IChatClient direto, sem Semantic Kernel. Microsoft.Extensions.AI (baixo nível)
        services.AddScoped<Client>(sp =>
        {
            var client = new Client();
            return client;
        });

        services.AddSingleton<Kernel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GeminiAIOptions>>().Value;

            var kernelBuilder = Kernel.CreateBuilder();

            // cria httpClient custom aqui (ou use IHttpClientFactory)
            var geminiHttpClient = GeminiHttpClientHelper.CreateGeminiHttpClient(
               ignoreSslErrors: environment.IsDevelopment());

            // Setup Google Gemini Embedding and Chat Completion
            kernelBuilder.AddGoogleAIEmbeddingGenerator(
                options.EmbeddingGeneratorModel!,
                apiKey!,
                apiVersion: GoogleAIVersion.V1,
                serviceId: "GeminiEmbedding",
                geminiHttpClient);

            kernelBuilder.AddGoogleAIGeminiChatCompletion
                (
                options.ChatModel!,
                apiKey!,
                apiVersion: GoogleAIVersion.V1,
                serviceId: "GeminiChat",
                geminiHttpClient);

            var kernel = kernelBuilder.Build();

            kernel.ImportPluginFromObject(sp.GetRequiredService<IProductKf>(), "Product");
            kernel.ImportPluginFromObject(sp.GetRequiredService<ITextProcessorKf>(), "TextProcessor");

            return kernel;
        });

        Log.Information("Gemini AI Client Configuration completed.");

        return services;
    }
}