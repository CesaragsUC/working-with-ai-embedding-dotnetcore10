using Demo.Embedding.Web.Configurations.ConfigsModel;
using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Embeddings;
using OpenAI.Images;
using Serilog;
using System.ClientModel;

namespace Demo.Embedding.Web;

/// <summary>
/// OpenAI SDK Doc: https://github.com/openai/openai-dotnet
/// </summary>
public static class OpenAIClientConfig
{
    public static IServiceCollection AddOpenAIClientConfig(
        this IServiceCollection services,
             IConfiguration configuration,
             IWebHostEnvironment environment)
    {
        Log.Information("Starting OpenAI Client Configuration...");

        services
        .AddOptions<OpenAIOptions>()
        .Bind(configuration.GetSection(OpenAIOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

        // OPENAI_API_KEY vem das variaveis de ambiente do sistema Recomendado para evitar vazar em codigo fonte.
        // Em producao salvar em Azure Key Vault ou similar.
        var apiKey = configuration["OPENAI_API_KEY"];

        services.AddSingleton<ChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
            return new ChatClient(options.ChatModel, apiKey);
        });

        services.AddSingleton<EmbeddingClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
            return new EmbeddingClient(options.EmbeddingGeneratorModel, apiKey);
        });

        services.AddSingleton<OpenAIClient>(sp =>
        {
            return new OpenAIClient(apiKey);
        });


        services.AddSingleton<ImageClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
            return new ImageClient(options.ImageModel, apiKey);
        });

        services.AddSingleton<AudioClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
            return new AudioClient(options.AudioModel, apiKey);
        });


        services.AddSingleton<Kernel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;

            var kernelBuilder = Kernel.CreateBuilder();

            // cria httpClient custom aqui (ou use IHttpClientFactory)
            var handler = new HttpClientHandler { CheckCertificateRevocationList = false };
            var httpClient = new HttpClient(handler);

            kernelBuilder.AddOpenAIChatCompletion(options.ChatModel!, apiKey!, serviceId: "OpenAIChat", httpClient: httpClient);

            var kernel = kernelBuilder.Build();

            kernel.ImportPluginFromObject(sp.GetRequiredService<IProductKf>(), "Product");
            kernel.ImportPluginFromObject(sp.GetRequiredService<ITextProcessorKf>(), "TextProcessor");

            return kernel;
        });

        Log.Information("OpenAI Client Configuration completed.");

        return services;
    }
}
