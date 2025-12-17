using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Wordprocessing;
using Google.GenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using OllamaSharp;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Demo.Embedding.Web;


[Route("api/[controller]")]
public class GeminiController(
            Kernel _kernel,
            AppEmbeddingDbContext context,
            Client geminiClient,
            UnifiedDocumentService documentService,
            GoogleAIGeminiChatCompletionService googleAIGeminiChat) : Controller
{

    [HttpPost]
    [Route("gemini-product-seed")]
    public async Task<IActionResult> ProductPromptGemini([FromServices] AppEmbeddingDbContext context)
    {
        var products = await context.Products.AsNoTracking().ToListAsync();

        // pega o serviço de embedding registrado no kernel
        var embeddingService = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>("GeminiEmbedding");

        var list = new List<ProductsRecomendation>();

        foreach (var item in products)
        {
            var embedding = await embeddingService.GenerateAsync(item.Description);

            list.Add(new ProductsRecomendation
            {
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                Category = item.Category,
                Embedding = new Vector(embedding.Vector),     // 768 (Gemini)
                EmbeddingLLM = null                    // opcional (ou deixa vazio)
            });
        }

        await context.ProductsRecomendation.AddRangeAsync(list);
        await context.SaveChangesAsync();

        return Ok(new { message = "Gemini seed completed successfully", count = list.Count });
    }


    /// <summary>
    /// Faz o seed de documentos para recomendação usando Gemini Embeddings
    /// </summary>
    /// <param name="document"></param>
    /// <returns></returns>

    [HttpPost]
    [Route("gemini-doc-seed")]
    public async Task<IActionResult> GeminiDocSeed([FromForm] DocumentAddDto document)
    {
        if (document.Document == null || document.Document.Length == 0)
            return BadRequest("Nenhum arquivo enviado");


        var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        Directory.CreateDirectory(uploadsPath);


        var filePath = Path.Combine(uploadsPath, document.Document.FileName);

        var docToEmbed = await documentService.ExtractTextFromStream(
                                         document.Document.OpenReadStream(),
                                         document.Document.FileName);


        var response = await geminiClient.Models.EmbedContentAsync(
                               model: "text-embedding-004",
                               contents: docToEmbed
                             );

        var embedding = response.Embeddings[0].Values.ToArray();

        var embeddingResult = new ReadOnlyMemory<float>(embedding.Select(x => (float)x).ToArray());


        var doc = new MyDemoDocument
        {
            Title = document.Title,
            DocText = docToEmbed,
        };

        await context.MyDemoDocuments.AddAsync(doc);
        await context.SaveChangesAsync();

        var docRecomendation = new MyDemoDocumentRecomendation
        {
            Title = document.Title,
            DocText = docToEmbed,
            DocumentId = doc.Id,
            Embedding = new Vector(embeddingResult)
        };

        await context.MyDemoDocumentRecomendation.AddAsync(docRecomendation);
        await context.SaveChangesAsync();

        return Ok(new { message = "Seed completed successfully" });
    }

    [HttpGet()]
    [Route("search-products")]
    public async Task<IActionResult> SearchProducts(
    [FromQuery] string q,
    [FromServices] AppEmbeddingDbContext db)
    {
        var embedder = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>("GeminiEmbedding");
        var qEmbedding = await embedder.GenerateAsync(q);

        var queryVector = new Vector(qEmbedding.Vector);

        // Se você tiver EF + Pgvector configurado, dá pra usar SQL raw:
        var results = await db.ProductSearchResults
            .FromSqlRaw("""
                SELECT
                    "Id",
                    "Name",
                    "Category",
                    "Price",
                    "Description"
                FROM product_recomendation
                WHERE "Embedding" IS NOT NULL
                ORDER BY "Embedding" <-> {0}
                LIMIT 5
            """, queryVector)
            .AsNoTracking()
            .ToListAsync();

        return Ok(results);



        return Ok(results);
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] string prompt)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();

        // Para o Gemini, você pode usar o GoogleAIPromptExecutionSettings (ou PromptExecutionSettings)
        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        ChatHistory history = new();
        history.AddUserMessage(prompt);

        await _kernel.InvokePromptAsync(prompt, new(settings));

        var response = await chat.GetChatMessageContentAsync(
            history,
            executionSettings: settings,
            kernel: _kernel);

        return Ok(response.Content);
    }



    [HttpGet]
    [Route("gemini-prod-kernel-recomendation")]
    public async Task<IActionResult> GeminiKernelDocPrompt(string prompt)
    {
        PromptExecutionSettings settings = new()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() // serve para habilitar funcoes se o modelo suportar
        };

        ChatHistory history = [];
        history.AddUserMessage(prompt);

        var response = await googleAIGeminiChat.GetChatMessageContentAsync(
                            chatHistory: history,
                            settings,
                            kernel: _kernel
                        );

        return Ok(response.Content);
    }

    /// <summary>
    ///  Gemini Example
    /// </summary>
    /// <param name="Prompt"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("gemini-product-recomendation")]
    public async Task<IActionResult> GeminiPrompt(string prompt)
    {

        var response = await geminiClient.Models.EmbedContentAsync(
                               model: "text-embedding-004",
                               contents: prompt
                             );

        var embedding = response.Embeddings[0].Values.ToArray();

        var embeddingResult = new ReadOnlyMemory<float>(embedding.Select(x => (float)x).ToArray());

        var vectorEmbedding = new Vector(embeddingResult);

        var productsRecomendations = await context.ProductsRecomendation
                                 .AsNoTracking()
                                 .OrderBy(e => e.Embedding.CosineDistance(vectorEmbedding))
                                 .Select(x => new { x.Id, x.Name, x.Description, x.Category, x.Price })
                                 .Take(1)//pega os 3 mais proximos. se quiser dar pra deixar 1 para pegar somente o mais proximo
                                 .ToListAsync();

        if (!productsRecomendations.Any())
        {
            return Ok(new
            {
                message = "Nenhum produto encontrado para essa busca",
                results = new List<object>()
            });
        }

        return Ok(productsRecomendations);
    }


    /// <summary>
    /// Prompt de recomendação de documentos usando Gemini Embeddings
    /// </summary>
    /// <param name="prompt"></param>
    /// <returns></returns>

    [HttpGet]
    [Route("gemini-doc-recomendation")]
    public async Task<IActionResult> GeminiDocPrompt(string prompt)
    {
        var response = await geminiClient.Models.EmbedContentAsync(
                               model: "text-embedding-004",
                               contents: prompt
                             );

        var embedding = response.Embeddings[0].Values.ToArray();

        var embeddingResult = new ReadOnlyMemory<float>(embedding.Select(x => (float)x).ToArray());

        var vectorEmbedding = new Vector(embeddingResult);

        var docRecomendations = await context.MyDemoDocumentRecomendation
                                 .AsNoTracking()
                                 .OrderBy(e => e.Embedding.CosineDistance(vectorEmbedding))
                                 .Select(x => new { x.Title, x.DocText, x.DocumentId })
                                 .Take(1)//pega os 3 mais proximos. se quiser dar pra deixar 1 para pegar somente o mais proximo
                                 .ToListAsync();

        if (!docRecomendations.Any())
        {
            return Ok(new
            {
                message = "Nenhum produto encontrado para essa busca",
                results = new List<object>()
            });
        }

        return Ok(docRecomendations);
    }

}

