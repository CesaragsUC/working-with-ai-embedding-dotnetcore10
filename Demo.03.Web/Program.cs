using Demo.Embedding.Web;
using Demo.Embedding.Web.Configurations;
using Demo.Embedding.Web.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Microsoft.SemanticKernel;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

// Start Serilog configuration to save in Seq logs
var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console() // Write logs to console
    .WriteTo.Seq(builder.Configuration["Serilog:SeqServerUrl"] ?? "http://localhost:5341") // Write to Seq server
    .CreateLogger();

Log.Logger = logger;

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IPdfPageRenderer, PdfPageRenderer>();

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

builder.Services.AddSingleton<UnifiedDocumentService>();

var envType = builder.Environment.IsDevelopment();
var envTypeName = builder.Environment.EnvironmentName.ToLower();

var handler = new HttpClientHandler
{
    // Evita falha por revogação offline (bem comum em redes corporativas)
    CheckCertificateRevocationList = false,
};

builder.Services.AddInfraStructureServices(builder.Configuration);
builder.Services.AddSingleton<IAutoFunctionInvocationFilter, FunctionFilter>();
builder.Services.AddScoped<IProductService, ProductService>();

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
