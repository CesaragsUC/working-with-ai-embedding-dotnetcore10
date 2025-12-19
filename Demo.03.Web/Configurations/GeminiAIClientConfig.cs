using DocumentFormat.OpenXml.Wordprocessing;
using Google.GenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;

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
        var kernelBuilder = Kernel.CreateBuilder();

        // var geminiHttpClient = GeminiHttpClientHelper.CreateGeminiHttpClient(ignoreSslErrors: environment.IsDevelopment());

        // GOOGLE_API_KEY vem das variaveis de ambiente do sistema Recomendado para evitar vazar em codigo fonte.
        // Em producao salvar em Azure Key Vault ou similar.
        var apiKey = configuration["GOOGLE_API_KEY"]
            ?? throw new InvalidOperationException("GOOGLE_API_KEY not configured");

        var chatModel = configuration.GetSection("GeminiAI:ChatModel").Value
                        ?? "gemini-2.5-flash";

        var embeddingModel = configuration.GetSection("GeminiAI:EmbeddingGeneratorModel").Value
                             ?? "text-embedding-004";

        //Alternativa IChatClient direto, sem Semantic Kernel. Microsoft.Extensions.AI (baixo nível)
        services.AddScoped<Client>(sp =>
        {
            var client = new Client();
            return client;
        });

        var geminiHttpClient = GeminiHttpClientHelper.CreateGeminiHttpClient(
          ignoreSslErrors: environment.IsDevelopment());

        // Setup Google Gemini Embedding and Chat Completion
        kernelBuilder.AddGoogleAIEmbeddingGenerator(
            embeddingModel!,
            apiKey!,
            apiVersion: GoogleAIVersion.V1,
            serviceId: "GeminiEmbedding",
            geminiHttpClient);

        kernelBuilder.AddGoogleAIGeminiChatCompletion
            (
            chatModel!,
            apiKey!,
            apiVersion: GoogleAIVersion.V1,
            serviceId: "GeminiChat",
            geminiHttpClient);

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