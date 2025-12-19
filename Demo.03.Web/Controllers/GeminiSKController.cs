using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OllamaSharp;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System.Diagnostics;

namespace Demo.Embedding.Web;

/// <summary>
///  Essa demo mostro como usar Gemini com Semantic Kernel
/// </summary>


[Route("api/[controller]")]
public class GeminiSKController : Controller
{
    private readonly Stopwatch _timer = new();
    private readonly Kernel _kernel;
    private readonly AppEmbeddingDbContext _context;
    private readonly UnifiedDocumentService _documentService;
    private readonly IPdfPageRenderer _pdfRenderer;

    public GeminiSKController(
        Kernel kernel,
        AppEmbeddingDbContext context,
        UnifiedDocumentService documentService,
        IPdfPageRenderer pdfPageRenderer)
    {
        _kernel = kernel;
        _context = context;
        _documentService = documentService;
        _pdfRenderer = pdfPageRenderer;
    }

    [HttpPost]
    [Route("product-seed")]
    public async Task<IActionResult> ProductPromptWithSk()
    {
        var products = await _context.Products.AsNoTracking().ToListAsync();

        // pega o serviço de embedding registrado no kernel
        var embeddingService = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>("GeminiEmbedding");

        var list = new List<ProductsRecomendation>();

        foreach (var item in products)
        {
            var embedding = await embeddingService.GenerateAsync(item.Description);

            list.Add(new ProductsRecomendation
            {
                ProductId = item.Id,
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                Category = item.Category,
                Embedding = new Vector(embedding.Vector),     // 768 (Gemini)
                EmbeddingLLM = null                    // opcional (ou deixa vazio)
            });
        }

        await _context.ProductsRecomendation.AddRangeAsync(list);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Gemini seed completed successfully", count = list.Count });
    }


    [HttpGet()]
    [Route("chat-products")]
    public async Task<IActionResult> SearchProductsWithSk(string prompt)
    {
        var embedder = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>("GeminiEmbedding");
        var qEmbedding = await embedder.GenerateAsync(prompt);

        var queryVector = new Vector(qEmbedding.Vector);

        //Operador <-> (distância de cosseno do pgvector)
        var results = await _context.ProductsRecomendation
            .AsNoTracking()
            .Where(p => p.Embedding != null)
            .OrderBy(e => e.Embedding!.CosineDistance(queryVector))
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.Category,
                x.Price
            })
            .Take(5)
            .ToListAsync();

        return Ok(results);

    }

    [HttpPost()]
    [Route("chat-functions")]
    public async Task<IActionResult> FunctionWithSk(string prompt)
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


    [HttpGet()]
    [Route("chat")]
    public async Task<IActionResult> ChatWithSk(string prompt)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();

        ChatHistory history = new();
        history.AddUserMessage(prompt);

        var response = await chat.GetChatMessageContentAsync(history);

        return Ok(response.Content);
    }

    [HttpPost()]
    [Route("chat-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> DescribeWithSk(
        [FromForm] DescribeImageRequest request,
        CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest("Nenhuma imagem enviada.");

        if (!request.File.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Envie um arquivo de imagem (image/*).");

        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await request.File.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        var chat = _kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddUserMessage(new ChatMessageContentItemCollection
        {
            new Microsoft.SemanticKernel.TextContent(request.Prompt),
            new ImageContent(bytes, request.File.ContentType) // bytes + mimeType (image/png, image/jpeg...)
        });

        var result = await chat.GetChatMessageContentAsync(history);

        return Ok(result.Content);
    }


    [HttpPost]
    [Route("chat-pdf")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GeminiDescribePdfTextPromptWithSk(
           [FromForm] DescribeImageRequest request,
           CancellationToken ct)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();

        if (request.File is null)
            return BadRequest("Nenhum arquivo enviado");

        if (!string.Equals(request.File.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest($"Arquivo não suportado: {request.File.FileName} ({request.File.ContentType})");

        byte[] fileBytes;
        await using (var ms = new MemoryStream())
        {
            await request.File.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
        }

        var pdfText = _documentService.ExtractTextFromPdf(fileBytes);

        // quebra o texto se for muito grande, ajuda a evitar erros de limite de tokens
        pdfText = pdfText.Length > 12000 ? pdfText[..12000] : pdfText;

        var history = new ChatHistory();
        history.AddUserMessage(new ChatMessageContentItemCollection
        {
            new Microsoft.SemanticKernel.TextContent(pdfText),
            new Microsoft.SemanticKernel.TextContent(request.Prompt)
        });

        var result = await chat.GetChatMessageContentAsync(history);

        return Ok(result.Content);
    }

    [HttpPost]
    [Route("chat-pdf-scanned")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GeminiDescribePdfScannedPromptWithSk(
          [FromForm] DescribeImageRequest request,
          CancellationToken ct)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();

        if (request.File is null)
            return BadRequest("Nenhum arquivo enviado");

        if (!string.Equals(request.File.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest($"Arquivo não suportado: {request.File.FileName} ({request.File.ContentType})");

        byte[] pdfBytes;
        await using (var ms = new MemoryStream())
        {
            await request.File.CopyToAsync(ms, ct);
            pdfBytes = ms.ToArray();
        }

        // Converte páginas para PNG (ex: 1 a 3 páginas pra teste)
        var pagePngs = await _pdfRenderer.SkiaSharpPdfRenderPagesAsPngListAsync(pdfBytes, maxPages: 3, ct);

        if (pagePngs.Count == 0)
            return BadRequest("Não consegui renderizar páginas desse PDF.");

        var history = new ChatHistory();

        history.AddUserMessage(new ChatMessageContentItemCollection
        {
            new Microsoft.SemanticKernel.TextContent(request.Prompt)
        });

        foreach (var pngBytes in pagePngs) // byte[] de cada página
        {
            history.AddUserMessage(new ChatMessageContentItemCollection
            {
                new ImageContent(pngBytes, "image/png")
            });
        }

        var result = await chat.GetChatMessageContentAsync(history);

        return Ok(result.Content);
    }
}

