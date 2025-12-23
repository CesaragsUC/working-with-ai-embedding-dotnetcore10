using Demo.Embedding.Web.Configurations.ConfigsModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Serilog;

namespace Demo.Embedding.Web;

public static class AzureAIClientConfig
{
    public static IServiceCollection AddAzureAIClientConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        Log.Information("Starting Azure AI Client Configuration...");

        // AZURE_AI_API_KEY vem das variaveis de ambiente do sistema Recomendado para evitar vazar em codigo fonte.
        // Em producao salvar em Azure Key Vault ou similar.
        var apiKey = configuration["AZURE_AI_API_KEY"];

        services
        .AddOptions<AzureAIOptions>()
        .Bind(configuration.GetSection(AzureAIOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

        services.AddSingleton<Kernel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureAIOptions>>().Value;

            var kernelBuilder = Kernel.CreateBuilder();

            // Setup AzureAI Embedding and Chat Completion
            #pragma warning disable SKEXP0010
            kernelBuilder.AddAzureOpenAIEmbeddingGenerator(
                deploymentName: options.ChatDeployment!,
                endpoint: options.Endpoint!,
                apiKey: apiKey!,
                serviceId: "AzureEmbedding",
                modelId: options.EmbeddingGeneratorModel
            );

            kernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName: options.ChatDeployment!,
                endpoint: options.Endpoint!,
                apiKey: apiKey!,
                serviceId: "AzureChat",
                modelId: options.ChatModel
            );

            var kernel = kernelBuilder.Build();

            // plugin criado via DI (não use AddFromType aqui)
            kernel.ImportPluginFromObject(sp.GetRequiredService<IProductKf>(), "Product");
            kernel.ImportPluginFromObject(sp.GetRequiredService<ITextProcessorKf>(), "TextProcessor");

            return kernel;
        });

        Log.Information("Anthropic AI Client Configuration completed.");

        return services;
    }
}