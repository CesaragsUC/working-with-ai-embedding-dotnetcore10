
using Demo.Embedding.Web;
using Google.GenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.OpenApi;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using OllamaSharp;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

// Start Serilog configuration
var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console() // Write logs to console
    .WriteTo.Seq(builder.Configuration["Serilog:SeqServerUrl"] ?? "http://localhost:5341") // Write to Seq server
    .CreateLogger();

Log.Logger = logger;

var geminiHttpClient = GeminiHttpClientHelper.CreateGeminiHttpClient(ignoreSslErrors: builder.Environment.IsDevelopment());


builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

var kernelBuilder = Kernel.CreateBuilder();

builder.Services.AddControllers();

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IPdfPageRenderer, PdfPageRenderer>();


// Para semanticKernel AddDbContextFactory é recomendado ao inves de AddDbContext. Fica melhor para usar os plugins
builder.Services.AddDbContextFactory<AppEmbeddingDbContext>(options =>
{

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrEmpty(connectionString))
        throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.UseVector();
    })
    .EnableSensitiveDataLogging(false)  // Desabilitar dados sensíveis
    .EnableDetailedErrors(false)        // Desabilitar erros detalhados
    .LogTo(Console.WriteLine, LogLevel.None); // Desabilitar logs completamente
});

builder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));

builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Fundamentos AI Embeddings", Version = "v1" }));
builder.Services.AddSingleton<UnifiedDocumentService>();

var envType = builder.Environment.IsDevelopment();
var envTypeName = builder.Environment.EnvironmentName.ToLower();

#region  Setup Semantic Kernel with Gemini - Gemini Client SDK - Ollama

// Doc Gemini Client SDK: https://googleapis.github.io/dotnet-genai/

var geminiApiKey = builder.Configuration["GOOGLE_API_KEY"]; // esse valor vem das variaveis de ambiente do sistema


// The client gets the API key from the environment variable `GEMINI_API_KEY`.
var client = new Client();
builder.Services.AddSingleton(client);


// Just to show alternative way to create the ChatClient for OpenAI ChatGPT.
var chatGptApiKey = builder.Configuration["OPENAI_API_KEY"];
var chatGptClient = new OpenAI.Chat.ChatClient(
                             model: "gpt-4o-mini",
                             apiKey: chatGptApiKey)
                            .AsIChatClient();

var ollamaApiClient = new OllamaApiClient("http://localhost:11434", "mxbai-embed-large:latest");
builder.Services.AddSingleton(ollamaApiClient);
builder.Services.AddSingleton(chatGptClient);

GoogleAIGeminiChatCompletionService chatCompletionService = new(
    modelId: "gemini-2.5-flash",
    apiKey: geminiApiKey,
    apiVersion: GoogleAIVersion.V1 // Optional
);

builder.Services.AddSingleton(chatCompletionService);

// Create singletons of your plugins
builder.Services.AddSingleton<ProductPlugin>();

var handler = new HttpClientHandler
{
    // Evita falha por revogação offline (bem comum em redes corporativas)
    CheckCertificateRevocationList = false,
};


// ambientes stage/prod usam Redis para cache de histórico de chat
if (!builder.Environment.IsDevelopment())
{
    // Setup Google Gemini Embedding and Chat Completion
    kernelBuilder.AddGoogleAIEmbeddingGenerator(
        "text-embedding-004",
        geminiApiKey,
        apiVersion: GoogleAIVersion.V1,
        serviceId: "GeminiEmbedding",
        geminiHttpClient);

    kernelBuilder.AddGoogleAIGeminiChatCompletion
        ("gemini-2.5-flash",
        geminiApiKey,
        apiVersion: GoogleAIVersion.V1,
        serviceId: "GeminiChat",
        geminiHttpClient);

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis");
        options.InstanceName = "ChatHistory:";
    });
}
else
{
    // Setup Ollama Embedding and Chat Completion
    kernelBuilder.AddOllamaEmbeddingGenerator(
        modelId: "mxbai-embed-large:latest",// supported models: mxbai-embed-large:latest
        endpoint: new Uri("http://localhost:11434"),
        serviceId: "OllamaEmbedding"
    );

    kernelBuilder.AddOllamaChatCompletion(
        modelId: "llama3.2:1b",// supported models: llama3.2:1b
        endpoint: new Uri("http://localhost:11434")
    );

}


builder.Services.AddSingleton<Kernel>(sp =>
{
    var kernel = kernelBuilder.Build();

    // plugin criado via DI (não use AddFromType aqui)
    kernel.Plugins.AddFromObject(sp.GetRequiredService<ProductPlugin>(), "ProductPlugin");

    return kernel;
});


#endregion

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
