using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Serilog;

namespace Demo.Embedding.Web;

public static class OpenAIClientConfig
{
    public static IServiceCollection AddOpenAIClientConfig(
        this IServiceCollection services,
             IConfiguration configuration,
             IWebHostEnvironment environment)
    {
        Log.Information("Starting OpenAI Client Configuration...");
        var kernelBuilder = Kernel.CreateBuilder();

        // OPENAI_API_KEY vem das variaveis de ambiente do sistema Recomendado para evitar vazar em codigo fonte.
        // Em producao salvar em Azure Key Vault ou similar.
        var apiKey = configuration["OPENAI_API_KEY"];
        var chatModel = configuration.GetSection("GeminiAI:ChatModel").Value;
        var embeddingModel = configuration.GetSection("OpenAI:EmbeddingGeneratorModel").Value;

        // Setup OpenAI Embedding and Chat Completion
        #pragma warning disable SKEXP0010
        kernelBuilder.AddOpenAIEmbeddingGenerator(
            embeddingModel!,
            apiKey!);

        kernelBuilder.AddOpenAIChatCompletion
        (
           chatModel!,
           apiKey!,
           serviceId: "OpenAIChat"
        );


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

        Log.Information("OpenAI Client Configuration completed.");

        return services;
    }
}
