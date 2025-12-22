using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using System.Diagnostics;

namespace Demo.Embedding.Web.Controllers;

public class AnthropicClientController : Controller
{
    private readonly Stopwatch _timer = new();
    private readonly IChatClient _chatClient;
    private readonly UnifiedDocumentService _documentService;
    private readonly IPdfPageRenderer _pdfRenderer;
    // private readonly Kernel _kernel;

    public AnthropicClientController(
        AppEmbeddingDbContext context,
        IChatClient chatClient,
        UnifiedDocumentService documentService,
        IPdfPageRenderer pdfPageRenderer)
    {
        _chatClient = chatClient;
        _documentService = documentService;
        _pdfRenderer = pdfPageRenderer;
        //  _kernel = kernel;   
    }

    [HttpGet]
    [Route("chatclient")]
    public async Task<IActionResult> ProductPrompt(string prompt)
    {

        ChatOptions options = new()
        {
            ModelId = AnthropicModels.Claude3Haiku,
            MaxOutputTokens = 512,
        };

        var response = await _chatClient.GetResponseAsync(prompt, options);

        return Ok(response.Text);
    }


    [HttpPost()]
    [Route("chat-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> DescribeImage(
        [FromForm] DescribeImageRequest request,
        CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest("Nenhuma imagem enviada.");

        if (!request.File.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Envie um arquivo de imagem (image/*).");

        byte[] imageBytes;
        var base64Image = string.Empty;
        var mediaType = request.File.ContentType;

        await using (var ms = new MemoryStream())
        {
            await request.File.CopyToAsync(ms, ct);
            imageBytes = ms.ToArray();
            base64Image = Convert.ToBase64String(imageBytes);
        }

        var response = await _chatClient.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User,
            [
                new DataContent(imageBytes, "image/jpeg"),
                new Microsoft.Extensions.AI.TextContent(request.Prompt),
            ])
        ], new()
        {
            ModelId = AnthropicModels.Claude45Haiku,
            MaxOutputTokens = 512,
            Temperature = 0f,
        });

        return Ok(response.Text);
    }

    [HttpPost]
    [Route("chat-pdf")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> DescribePdfPrompt(
           [FromForm] DescribeImageRequest request,
           CancellationToken ct)
    {
        if (request.File is null)
            return BadRequest("Nenhum arquivo enviado");

        if (!string.Equals(request.File.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest($"Arquivo não suportado: {request.File.FileName} ({request.File.ContentType})");

        byte[] fileBytes;
        var base64File = string.Empty;
        await using (var ms = new MemoryStream())
        {
            await request.File.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
            base64File = Convert.ToBase64String(fileBytes);
        }

        var _anthropicClient = new AnthropicClient();
        var messages = new List<Message>()
        {
            new Message(RoleType.User, new DocumentContent()
            {
                Source = new DocumentSource()
                {
                    Type = SourceType.base64,
                    Data = base64File,
                    MediaType = "application/pdf"
                },
                CacheControl = new CacheControl()
                {
                    Type = CacheControlType.ephemeral
                }
            }),
            new Message(RoleType.User, request.Prompt),
        };

        var parameters = new MessageParameters()
        {
            Messages = messages,
            MaxTokens = 1024,
            Model = AnthropicModels.Claude45Haiku,
            Stream = false,
            Temperature = 0m,
            PromptCaching = PromptCacheType.FineGrained
        };
        var response = await _anthropicClient.Messages.GetClaudeMessageAsync(parameters);

        // retorna a resposta direta
        var chatReponse = response.FirstMessage.Text;

        //retorna o objeto completo de resposta
        return Ok(response.Message.Content.FirstOrDefault());
    }


    [HttpPost]
    [Route("chat-pdf-scanned")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> DescribePdfScannedPrompt(
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
        var imageBytes = await _pdfRenderer.SkiaSharpPdfRenderPagesAsPngListAsync(pdfBytes, maxPages: 3, ct);

        if (imageBytes.Count == 0)
            return BadRequest("Não consegui renderizar páginas desse PDF.");

        var chatMessages = new List<ChatMessage>();
        ChatOptions option = new()
        {
            ModelId = AnthropicModels.Claude45Haiku,
            MaxOutputTokens = 512,
            Temperature = 0f,
        };

        foreach (var imageByte in imageBytes)
        {
            var message = new ChatMessage(ChatRole.User,
            [
                new DataContent(imageByte, "image/png"),
                new Microsoft.Extensions.AI.TextContent(request.Prompt),
            ]);

            chatMessages.Add(message);
        }

        var response = await _chatClient.GetResponseAsync(chatMessages, option);

        return Ok(response.Text);
    }
}
