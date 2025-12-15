
using Demo.Embedding.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Microsoft.SemanticKernel;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

var kernelBuilder = Kernel.CreateBuilder();

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppEmbeddingDbContext>(options => {

    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),p => p.UseVector());
});

builder.Services.AddScoped<AppEmbeddingDbContext>();

var ollamaApiClient = new OllamaApiClient("http://localhost:11434", "mxbai-embed-large:latest");
builder.Services.AddSingleton(ollamaApiClient);


builder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));

builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Fundamentos AI Embeddings", Version = "v1" }));


var uri = new Uri("http://localhost:11434");

// Model Config
kernelBuilder.AddOllamaEmbeddingGenerator(
    modelId: "mxbai-embed-large:latest",// supported models: mxbai-embed-large:latest
    endpoint: uri,
    serviceId: "OllamaEmbedding"
);

// Embedding Config
kernelBuilder.AddOllamaChatCompletion(
    modelId: "llama3.2:1b",// supported models: llama3.2:1b
    endpoint: uri
);

if (builder.Environment.IsDevelopment())
{
    //builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis");
        options.InstanceName = "ChatHistory:";
    });
}

var kernel = kernelBuilder.Build();

kernel.Plugins.AddFromType<ProductPlugin>("Products");

// Registrar no DI
builder.Services.AddSingleton(kernel);

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
