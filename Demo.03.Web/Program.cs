using Demo.Embedding.Web;
using Demo.Embedding.Web.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders(); // evita Console/Debug duplicados

// Start Serilog configuration to save in Seq logs
var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

Log.Logger = logger;
builder.Host.UseSerilog();
Log.Information("Starting application...");
Log.Information("Environment: {Environment}", builder.Environment.EnvironmentName);

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IPdfPageRenderer, PdfPageRenderer>();
builder.Services.AddSingleton<UnifiedDocumentService>();

builder.Services.AddInfraStructureServices(builder.Configuration);
builder.Services.AddScoped<IProductKf, ProductKf>();
builder.Services.AddScoped<ITextProcessorKf, TextProcessorKf>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Fundamentos AI Embeddings",
        Version = "v1"
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

//Aumentar limite de request body
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue;
});

var envType = builder.Environment.IsDevelopment();
var envTypeName = builder.Environment.EnvironmentName.ToLower();

var handler = new HttpClientHandler
{
    // Evita falha por revogação offline (bem comum em redes corporativas)
    CheckCertificateRevocationList = false,
};


if (builder.Environment.IsDevelopment())
{
    // builder.Services.AddDistributedMemoryCache();
    builder.Services.AddOllamaAIClientConfig(builder.Configuration, builder.Environment);
}
else
{
    builder.Services.AddGeminiAIClientConfig(builder.Configuration, builder.Environment);

    // ambientes stage/prod usam Redis para cache de histórico de chat caso precise.
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis");
        options.InstanceName = "ChatHistory:";
    });
}

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
