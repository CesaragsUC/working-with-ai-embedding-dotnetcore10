using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace Demo.Embedding.Web;

public static class OllamaAIClientConfig
{
    public static IServiceCollection AddOllamaAIClientConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var kernelBuilder = Kernel.CreateBuilder();

        var serverEndpoint = configuration.GetSection("OllamaAI:ServerUrl").Value;
        var embeddingModel = configuration.GetSection("OllamaAI:EmbeddingGeneratorModel").Value;
        var chatModel = configuration.GetSection("OllamaAI:ChatModel").Value;


        // Setup Ollama Embedding and Chat Completion
        kernelBuilder.AddOllamaEmbeddingGenerator(
            modelId: chatModel!,
            endpoint: new Uri(serverEndpoint!),
            serviceId: "OllamaEmbedding"
        );

        kernelBuilder.AddOllamaChatCompletion(
            modelId: chatModel!,
            endpoint: new Uri(serverEndpoint!),
            serviceId: "OllamaChat"
        );


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
