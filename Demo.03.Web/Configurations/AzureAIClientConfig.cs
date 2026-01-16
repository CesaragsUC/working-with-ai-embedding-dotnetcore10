using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Demo.Embedding.Web.Configurations.ConfigsModel;
using Demo.Embedding.Web.Services;
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

public static class AzureAIClientConfig
{
    public static IServiceCollection AddAzureAIClientConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        Log.Information("Starting Azure AI Client Configuration...");

        // API_KEYS vem das variaveis de ambiente do sistema Recomendado para evitar vazar em codigo fonte.
        // Em producao salvar em Azure Key Vault ou similar.
        var gptApiKey = configuration["AZURE_GPT_O_4_MINI_API_KEY"];
        var dallEApiKey = configuration["AZURE_DALL_E_3_API_KEY"];
        var embeddingApiKey = configuration["AZURE_EMBEDDING_3_SMALL_API_KEY"];
        var soraApiKey = configuration["AZURE_SORA_API_KEY"];
        var whisperApiKey = configuration["AZURE_WISPER_API_KEY"];


        services
        .AddOptions<AzureAIOptions>()
        .Bind(configuration.GetSection(AzureAIOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();


        services.AddScoped<IAzureSoraService, AzureSoraService>();

        services.AddSingleton<AzureOpenAIClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureAIOptions>>().Value;
            return new AzureOpenAIClient(options.Endpoint, new AzureKeyCredential(gptApiKey!));
        });

        services.AddSingleton<ChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureAIOptions>>().Value;

            ChatClient client = new(
            credential: new ApiKeyCredential(gptApiKey!),
            model: options.ChatModel,
            options: new OpenAIClientOptions()
            {
                Endpoint = new($"{options.Endpoint}"),
            });

            return client;
        });

        services.AddSingleton<EmbeddingClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureAIOptions>>().Value;
            return new EmbeddingClient(
                options.EmbeddingGeneratorModel,
                new AzureKeyCredential(embeddingApiKey!),
                new OpenAIClientOptions() { Endpoint = options.Endpoint });
        });

        services.AddSingleton<ImageClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureAIOptions>>().Value;

            ImageClient client = new(
                model: options.ImageModel,
                credential: new ApiKeyCredential(dallEApiKey!),
                options: new OpenAIClientOptions() { Endpoint = options.Endpoint }
            );

            return client;
        });

        services.AddSingleton<AudioClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureAIOptions>>().Value;

            AudioClient client = new(
                model: options.ImageModel,
                credential: new ApiKeyCredential(whisperApiKey!),
                options: new OpenAIClientOptions() { Endpoint = options.Endpoint }
            );

            return client;
        });

        services.AddSingleton<Kernel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureAIOptions>>().Value;

            var kernelBuilder = Kernel.CreateBuilder();

            // Setup AzureAI Embedding and Chat Completion
            #pragma warning disable SKEXP0010
            kernelBuilder.AddAzureOpenAIEmbeddingGenerator(
                deploymentName: options.EmbeddingGeneratorModel!,
                endpoint: options.EmbeddingEndpoint!,
                apiKey: embeddingApiKey!,
                serviceId: "AzureEmbedding",
                modelId: options.EmbeddingGeneratorModel
            );

            kernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName: options.ChatModel!,
                endpoint: options.ChatModelEndpoint!,
                apiKey: gptApiKey!,
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