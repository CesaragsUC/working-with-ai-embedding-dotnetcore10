using Demo.Embedding.Web.Configurations.ConfigsModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Serilog;

namespace Demo.Embedding.Web;

public static class OllamaAIClientConfig
{
    public static IServiceCollection AddOllamaAIClientConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        Log.Information("Starting Ollama AI Client Configuration...");

        services
        .AddOptions<OllamaAIOptions>()
        .Bind(configuration.GetSection(OllamaAIOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

        services.AddSingleton<Kernel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OllamaAIOptions>>().Value;

            var kernelBuilder = Kernel.CreateBuilder();

            // Setup Ollama Embedding and Chat Completion
            kernelBuilder.AddOllamaEmbeddingGenerator(
                modelId: options.EmbeddingGeneratorModel!,
                endpoint: new Uri(options.ServerUrl!),
                serviceId: "OllamaEmbedding"
            );

            kernelBuilder.AddOllamaChatCompletion(
                modelId: options.ChatModel!,
                endpoint: new Uri(options.ServerUrl!),
                serviceId: "OllamaChat"
            );

            var kernel = kernelBuilder.Build();

            kernel.ImportPluginFromObject(sp.GetRequiredService<IProductKf>(), "Product");
            kernel.ImportPluginFromObject(sp.GetRequiredService<ITextProcessorKf>(), "TextProcessor");

            return kernel;
        });

        Log.Information("Ollama AI Client Configuration completed.");

        return services;
    }
}
