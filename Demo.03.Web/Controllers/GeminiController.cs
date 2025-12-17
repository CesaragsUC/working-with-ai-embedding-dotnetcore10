using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using OllamaSharp;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Serilog;
using System.Diagnostics;

namespace Demo.Embedding.Web;

/// <summary>
///  Essa demo mostro como usar Gemini com Semantic Kernel e Gemini Client SDK (implementacao direta)
/// </summary>


[Route("api/[controller]")]
public class GeminiController : Controller
{
    private readonly Stopwatch _timer = new();
    private readonly Kernel _kernel;
    private readonly AppEmbeddingDbContext _context;
    private readonly Client _geminiClient;
    private readonly UnifiedDocumentService _documentService;
    private readonly GoogleAIGeminiChatCompletionService _googleAIGeminiChat;
    private readonly IPdfPageRenderer _pdfRenderer;

    public GeminiController(Kernel kernel,
        AppEmbeddingDbContext context,
        Client geminiClient,
        UnifiedDocumentService documentService,
        GoogleAIGeminiChatCompletionService googleAIGeminiChat,
        IPdfPageRenderer pdfPageRenderer)
    {
        _kernel = kernel;
        _context = context;
        _geminiClient = geminiClient;
        _documentService = documentService;
        _googleAIGeminiChat = googleAIGeminiChat;
        _pdfRenderer = pdfPageRenderer;
    }


    [HttpPost]
    [Route("gemini-upload-pdf-gemini-client")]
    public async Task<IActionResult> GeminiDocSeed([FromForm] DocumentAddDto document)
    {
        if (document.Document == null || document.Document.Length == 0)
            return BadRequest("Nenhum arquivo enviado");


        var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        Directory.CreateDirectory(uploadsPath);


        var filePath = Path.Combine(uploadsPath, document.Document.FileName);

        var docToEmbed = await _documentService.ExtractTextFromStream(
                                         document.Document.OpenReadStream(),
                                         document.Document.FileName);


        var response = await _geminiClient.Models.EmbedContentAsync(
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

        await _context.MyDemoDocuments.AddAsync(doc);
        await _context.SaveChangesAsync();

        var docRecomendation = new MyDemoDocumentRecomendation
        {
            Title = document.Title,
            DocText = docToEmbed,
            DocumentId = doc.Id,
            Embedding = new Vector(embeddingResult)
        };

        await _context.MyDemoDocumentRecomendation.AddAsync(docRecomendation);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Seed completed successfully" });
    }

    [HttpGet]
    [Route("gemini-pdf-search-gemini-client")]
    public async Task<IActionResult> GeminiDocPrompt(string prompt)
    {
        var response = await _geminiClient.Models.EmbedContentAsync(
                               model: "text-embedding-004",
                               contents: prompt
                             );

        var embedding = response.Embeddings[0].Values.ToArray();

        var embeddingResult = new ReadOnlyMemory<float>(embedding.Select(x => (float)x).ToArray());

        var vectorEmbedding = new Vector(embeddingResult);

        var docRecomendations = await _context.MyDemoDocumentRecomendation
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


    [HttpPost]
    [Route("describe-pdf-text-gemini-client")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GeminiDescribePdfPrompt(
           [FromForm] DescribeImageRequest request,
           CancellationToken ct)
    {
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

        var parts = new List<Part>
        {
            new Part { Text = request.Prompt },
            new Part { Text = pdfText }
        };

        var response = await _geminiClient.Models.GenerateContentAsync(
            model: "gemini-2.5-flash",
            contents: new List<Content>
            {
               new Content { Role = "user", Parts = parts }
            });

        return Ok(response.Candidates[0].Content.Parts[0].Text);
    }


    [HttpPost]
    [Route("describe-pdf-scanned-gemini-client")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GeminiDescribePdfScannedPrompt(
       [FromForm] DescribeImageRequest request,
        CancellationToken ct)
    {
        if (request.File is null) return BadRequest("Nenhum arquivo enviado");
        if (!string.Equals(request.File.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Envie um PDF.");

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

        var parts = new List<Part>
        {
            new Part { Text = request.Prompt ?? "Describe what you see in these PDF pages:" }
        };

        foreach (var pngBytes in pagePngs)
        {
            parts.Add(new Part
            {
                InlineData = new Blob
                {
                    MimeType = "image/png",
                    Data = pngBytes
                }
            });
        }

        var resp = await _geminiClient.Models.GenerateContentAsync(
            model: "gemini-2.5-flash",
            contents: new List<Content> { new Content { Role = "user", Parts = parts } }
        );

        return Ok(resp.Candidates[0].Content.Parts[0].Text);
    }

    [HttpGet]
    [Route("gemini-product-search-gemini-client")]
    public async Task<IActionResult> GeminiPrompt(string prompt)
    {

        var response = await _geminiClient.Models.EmbedContentAsync(
                               model: "text-embedding-004",
                               contents: prompt
                             );

        var embedding = response.Embeddings[0].Values.ToArray();

        var embeddingResult = new ReadOnlyMemory<float>(embedding.Select(x => (float)x).ToArray());

        var vectorEmbedding = new Vector(embeddingResult);

        var productsRecomendations = await _context.ProductsRecomendation
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



    [HttpPost]
    [Route("describe-image-gemini-client")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GeminiDescribeImagePrompt(
       [FromForm] DescribeImageRequest request,
        CancellationToken ct)
    {
        var imagesBase64 = string.Empty;
        byte[] imagesBytes;
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var parts = new List<Part>();

        // texto do usuário
        parts.Add(new Part { Text = request.Prompt });

        if (request.File != null)
        {
            using var ms = new MemoryStream();
            await request.File.CopyToAsync(ms, ct);
            imagesBytes = ms.ToArray();

            parts.Add(new Part
            {
                InlineData = new Blob
                {
                    MimeType = request.File.ContentType, // ex: image/png, image/jpeg
                    Data = imagesBytes
                }
            });

            if (request.File.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                imagesBase64 = Convert.ToBase64String(imagesBytes, Base64FormattingOptions.None);
            }
            else
            {
                return BadRequest($"[Arquivo não suportado no momento: {request.File.FileName} ({request.File.ContentType})]");
            }

            var response = await _geminiClient.Models.GenerateContentAsync(
                model: "gemini-2.5-flash",
                contents: new List<Content>
                {
                   new Content
                   {
                      Role = "user",
                      Parts = parts
                   }
                });


            Log.Warning(@"Process with file took {Elapsed:hh\:mm\:ss\.fff}", _timer.Elapsed);

            return Ok(response.Candidates[0].Content.Parts[0].Text);
        }
        return BadRequest("Nenhum arquivo enviado");
    }


    [HttpPost]
    [Route("product-seed-semantic-kernel")]
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
    [Route("search-products-semantic-kernel")]
    public async Task<IActionResult> SearchProductsWithSk(string prompt)
    {
        var embedder = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>("GeminiEmbedding");
        var qEmbedding = await embedder.GenerateAsync(prompt);

        var queryVector = new Vector(qEmbedding.Vector);

        // Se você tiver EF + Pgvector configurado, dá pra usar SQL raw:
        var results = await _context.ProductSearchResults
            .FromSqlRaw("""
                SELECT
                    "Id",
                    "ProductId",
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

    }

    [HttpGet()]
    [Route("function-semantic-kernel")]
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
    [Route("basic-chat-semantic-kernel")]
    public async Task<IActionResult> ChatWithSk(string prompt)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();

        ChatHistory history = new();
        history.AddUserMessage(prompt);

        var response = await chat.GetChatMessageContentAsync(history);

        return Ok(response.Content);
    }

    [HttpPost()]
    [Route("describe-image-semantic-kernel")]
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
    [Route("describe-pdf-text-semantic-kernel")]
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
    [Route("describe-pdf-scanned-semantic-kernel")]
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

