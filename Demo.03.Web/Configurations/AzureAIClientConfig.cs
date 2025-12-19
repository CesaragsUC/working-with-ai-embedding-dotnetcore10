using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace Demo.Embedding.Web;

public static class AzureAIClientConfig
{
    public static IServiceCollection AddAzureAIClientConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var kernelBuilder = Kernel.CreateBuilder();

        // AZURE_AI_API_KEY vem das variaveis de ambiente do sistema Recomendado para evitar vazar em codigo fonte.
        // Em producao salvar em Azure Key Vault ou similar.
        var apiKey = configuration["AZURE_AI_API_KEY"];

        // nomes dos deployments que VOCÊ criou no Azure
        var deployment = configuration["AzureAI:ChatDeployment"];
        var embeddingDeployment = configuration["AzureAI:EmbeddingDeployment"];
        var endpoint = configuration["AzureAI:Endpoint"];
        var embeddingModel = configuration.GetSection("AzureAI:EmbeddingGeneratorModel").Value;
        var chatModel = configuration.GetSection("AzureAI:ChatModel").Value;

        // Setup AzureAI Embedding and Chat Completion
        #pragma warning disable SKEXP0010
        kernelBuilder.AddAzureOpenAIEmbeddingGenerator(
            deploymentName: deployment!,
            endpoint: endpoint!,
            apiKey: apiKey!,
            serviceId: "AzureEmbedding",
            modelId: embeddingModel
        );

        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: deployment!,
            endpoint: endpoint!,
            apiKey: apiKey!,
            serviceId: "AzureChat",
            modelId: chatModel
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