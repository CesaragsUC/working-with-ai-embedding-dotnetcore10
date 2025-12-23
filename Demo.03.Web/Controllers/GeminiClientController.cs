using Azure;
using Demo.Embedding.Web.Utils;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Serilog;
using System.Diagnostics;

namespace Demo.Embedding.Web;

/// <summary>
///  Essa demo mostro como usar Gemini Client SDK (implementacao direta baixo nivel)
///  Pacote: https://github.com/googleapis/dotnet-genai
/// </summary>


[Route("api/[controller]")]
public class GeminiClientController : Controller
{
    private readonly Stopwatch _timer = new();
    private readonly AppEmbeddingDbContext _context;
    private readonly Client _geminiClient;
    private readonly UnifiedDocumentService _documentService;
    private readonly IPdfPageRenderer _pdfRenderer;

    public GeminiClientController(
        AppEmbeddingDbContext context,
        Client geminiClient,
        UnifiedDocumentService documentService,
        IPdfPageRenderer pdfPageRenderer)
    {
        _context = context;
        _geminiClient = geminiClient;
        _documentService = documentService;
        _pdfRenderer = pdfPageRenderer;
    }

    [HttpPost]
    [Route("generate-image")]
    public async Task<IActionResult> GenerateImage(string prompt)
    {
        var generateImagesConfig = new GenerateImagesConfig
        {
            NumberOfImages = 1,
            AspectRatio = "1:1",
            SafetyFilterLevel = SafetyFilterLevel.BLOCK_LOW_AND_ABOVE,
            PersonGeneration = PersonGeneration.DONT_ALLOW,
            IncludeSafetyAttributes = true,
            IncludeRaiReason = true,
            OutputMimeType = "image/jpeg",
        };

        var response = await _geminiClient.Models.GenerateImagesAsync(
          model: "imagen-4.0-generate-001",
          prompt: prompt,
          config: generateImagesConfig
        );

        // Do something with the generated image
        var image = response.GeneratedImages.First().Image;

      //  var fileName = $"image{Guid.NewGuid()}.jpg";

       // var savedPath = FileDownloadHelper.SaveToDownloads(fileName, image.ImageBytes);

        var downloadsPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads"
        );

        var outputPath = Path.Combine(downloadsPath, $"image_generated_{Guid.NewGuid()}.png");

       // var image = response.GeneratedImages.First().Image;
        var imageBytes = image.ImageBytes.ToArray();

        // Salvar bytes no arquivo
        await System.IO.File.WriteAllBytesAsync(outputPath, imageBytes);

        return Ok(new { Message = "Imagem gerada com sucesso!", ImagePath = outputPath });
    }

    [HttpPost]
    [Route("generate-video")]
    public async Task<IActionResult> GenerateVideo(string prompt)
    {
        var source = new GenerateVideosSource
        {
            Prompt = prompt,
        };

        var config = new GenerateVideosConfig
        {
            NumberOfVideos = 1,
        };
        var operation = await _geminiClient.Models.GenerateVideosAsync(
          model: "veo-3.1-generate-preview", source: source, config: config);

        while (operation.Done != true)
        {
            try
            {
                await Task.Delay(10000);
                operation = await _geminiClient.Operations.GetAsync(operation, null);
            }
            catch (TaskCanceledException)
            {
                Log.Error("Task was cancelled while waiting.");
                break;
            }
        }
        // Do something with the generated video
        var downloadsPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads"
        );

        var outputPath = Path.Combine(downloadsPath, $"video_generated_{Guid.NewGuid()}.mp4");

        await _geminiClient.Files.DownloadToFileAsync(
            generatedVideo: operation.Response.GeneratedVideos.First(),
            outputPath: outputPath
        );

        return Ok(new { Message = "Video gerado com sucesso!", VideoPath = outputPath });
    }

    [HttpPost]
    [Route("generate-video-from-image")]
    public async Task<IActionResult> GenerateVideoFromImage([FromForm] DescribeImageRequest request)
    {

        // 2. Create a temporary file path with the correct extension
        // The SDK needs a real path, and sometimes checks the extension (e.g. .png, .jpg)
        var tempFile = Path.GetTempFileName();
        var tempPathWithExtension = Path.ChangeExtension(tempFile, Path.GetExtension(request.File.FileName));

        // Rename the 0-byte temp file to include the extension
        System.IO.File.Move(tempFile, tempPathWithExtension);

        // 3. Save the uploaded file (RAM) to the temporary path (Disk)
        using (var stream = new FileStream(tempPathWithExtension, FileMode.Create))
        {
            await request.File.CopyToAsync(stream);
        }

        var source = new GenerateVideosSource
        {
            Prompt = request.Prompt,
            Image = Image.FromFile(tempPathWithExtension),
        };

        var config = new GenerateVideosConfig
        {
            NumberOfVideos = 1,
        };

        var operation = await _geminiClient.Models.GenerateVideosAsync(
          model: "veo-3.1-generate-preview", source: source, config: config);

        while (operation.Done != true)
        {
            try
            {
                await Task.Delay(10000);
                operation = await _geminiClient.Operations.GetAsync(operation, null);
            }
            catch (TaskCanceledException)
            {
                Log.Error("Task was cancelled while waiting.");
                break;
            }
        }

        // Do something with the generated video
        var downloadsPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads"
        );

        var outputPath = Path.Combine(downloadsPath, $"video_generated_{Guid.NewGuid()}.mp4");

        await _geminiClient.Files.DownloadToFileAsync(
            generatedVideo: operation.Response.GeneratedVideos.First(),
            outputPath: outputPath
        );

        return Ok(new { Message = "Video gerado com sucesso!", VideoPath = outputPath });
    }


    [HttpPost]
    [Route("upload-pdf")]
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
    [Route("chat-document")]
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
    [Route("chat-pdf")]
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
    [Route("chat-pdf-scanned")]
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
    [Route("chat-product")]
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
                                 .Take(3)//pega os 3 mais proximos. se quiser dar pra deixar 1 para pegar somente o mais proximo
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

    [HttpGet]
    [Route("product-recomendation")]
    public async Task<IActionResult> ProductRecomentadion(string prompt)
    {
        var parts = new List<Part>
        {
            new Part { Text = prompt }
        };

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

        return Ok(response.Candidates[0].Content.Parts[0].Text);
    }

    [HttpPost]
    [Route("chat-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GeminiDescribeImagePrompt(
       [FromForm] DescribeImageRequest request,
        CancellationToken ct)
    {
        var imagesBase64 = string.Empty;
        byte[] imagesBytes;
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

}

