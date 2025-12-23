using Demo.Embedding.Web.Configurations.ConfigsModel;
using Google.GenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Embeddings;
using OpenAI.Files;
using OpenAI.Images;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System.Diagnostics;

namespace Demo.Embedding.Web.Controllers;

[Route("api/[controller]")]
public class OpenAISkController : Controller
{
    private readonly Stopwatch _timer = new();
    private readonly ChatClient _chatClient;
    private readonly AudioClient _audioClient;
    private readonly EmbeddingClient _embeddingClient;
    private readonly OpenAIClient _openAIClient;
    private readonly UnifiedDocumentService _documentService;
    private readonly IPdfPageRenderer _pdfRenderer;
    private readonly Kernel _kernel;
    private readonly ImageClient _imageClient;
    private readonly AppEmbeddingDbContext _context;
    private readonly OpenAIOptions _openAiOptions;

    public OpenAISkController(
        AppEmbeddingDbContext context,
        Kernel kernel,
        ChatClient chatClient,
        EmbeddingClient embeddingClient,
        OpenAIClient openAIClient,
        AudioClient audioClient,
        ImageClient imageClient,
        UnifiedDocumentService documentService,
        IPdfPageRenderer pdfPageRenderer,
        IOptions<OpenAIOptions> options)
    {
        _chatClient = chatClient;
        _embeddingClient = embeddingClient;
        _documentService = documentService;
        _pdfRenderer = pdfPageRenderer;
        _openAiOptions = options.Value;
        _kernel = kernel;
        _imageClient = imageClient;
        _context = context;
        _audioClient = audioClient;
        _openAIClient = openAIClient;
    }

    [HttpGet]
    [Route("chatclient")]
    public async Task<IActionResult> ProductPrompt(string prompt)
    {
        ChatCompletion completion = await _chatClient.CompleteChatAsync(prompt);

        return Ok(new { response = completion.Content[0].Text });
    }

    [HttpGet]
    [Route("describe-audio")]
    public async Task<IActionResult> DescribeAudio([FromForm] ChatRequest request)
    {
        // 2. Create a temporary file path with the correct extension
        // The SDK needs a real path, and sometimes checks the extension (e.g. .mp3, .wav)
        var tempFile = Path.GetTempFileName();
        var audioFilePath = Path.ChangeExtension(tempFile, Path.GetExtension(request.File.FileName));

        // Rename the 0-byte temp file to include the extension
        System.IO.File.Move(tempFile, audioFilePath);

        // 3. Save the uploaded file (RAM) to the temporary path (Disk)
        using (var stream = new FileStream(audioFilePath, FileMode.Create))
        {
            await request.File.CopyToAsync(stream);
        }

        AudioTranscriptionOptions options = new()
        {
            ResponseFormat = AudioTranscriptionFormat.Verbose,
            TimestampGranularities = AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment,
        };

        AudioTranscription transcription = _audioClient.TranscribeAudio(audioFilePath, options);

        return Ok(new { response = transcription.Text });
    }


    [HttpGet]
    [Route("generate-image")]
    public async Task<IActionResult> GenerateImage(string prompt)
    {
        OpenAI.Images.ImageGenerationOptions options = new()
        {
            Quality = GeneratedImageQuality.High,
            Size = GeneratedImageSize.W1792xH1024,
            Style = GeneratedImageStyle.Vivid,
            ResponseFormat = GeneratedImageFormat.Bytes
        };

        GeneratedImage image = _imageClient.GenerateImage(prompt, options);
        BinaryData bytes = image.ImageBytes;

        var downloadsPath = Path.Combine(
           System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads"
        );

        var outputPath = Path.Combine(downloadsPath, $"openai-image_generated_{Guid.NewGuid()}.png");

        // var image = response.GeneratedImages.First().Image;
        var imageBytes = image.ImageBytes.ToArray();

        // Salvar bytes no arquivo
        await System.IO.File.WriteAllBytesAsync(outputPath, imageBytes);

        return Ok(new { Message = "Imagem gerada com sucesso!", ImagePath = outputPath });
    }


    [HttpGet()]
    [Route("chat-embedded-products")]
    public async Task<IActionResult> SearchProductsWithSk(string prompt)
    {

        OpenAIEmbedding embedding =  await  _embeddingClient.GenerateEmbeddingAsync(prompt);
        ReadOnlyMemory<float> vector = embedding.ToFloats();

        var queryVector = new Vector(vector);

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

    [HttpPost]
    [Route("function-textprocessor")]
    public async Task<IActionResult> AutoInvocation([FromQuery] string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            return BadRequest(new { error = "Prompt is null or empty" });
        }

        //Configura para usar uma função específica
        KernelFunction functionSk = _kernel.Plugins.GetFunction("TextProcessor", "ToUpper");

        GeminiPromptExecutionSettings prompSettings = new()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };

        var result = await _kernel.InvokePromptAsync(prompt, new KernelArguments(prompSettings));

        return Ok(new { response = result.ToString() });
    }


    [HttpPost()]
    [Route("function-product")]
    public async Task<IActionResult> FunctionWithSk(string prompt)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();

        if (string.IsNullOrEmpty(prompt))
        {
            return BadRequest(new { error = "Prompt is null or empty" });
        }

        //Configura para usar uma função específica
        KernelFunction functionSk = _kernel.Plugins.GetFunction("Product", "get_product_by_id");

        PromptExecutionSettings prompSettings = new()
        {
            //FunctionChoiceBehavior = FunctionChoiceBehavior.Required(functions: new[] { functionSk }),
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),

        };

        var history = new ChatHistory();

        history.AddUserMessage(prompt);

        var resultChat = await chat.GetChatMessageContentAsync(
           prompt,
           executionSettings: prompSettings,
           kernel: _kernel
        );


        var result = await _kernel.InvokePromptAsync(prompt, new KernelArguments(prompSettings));


        // return Ok(new { response = resultChat.Content });
        return Ok(new { response = result.ToString() });

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

        // 2. Create a temporary file path with the correct extension
        // The SDK needs a real path, and sometimes checks the extension (e.g. .mp3, .wav)
        var tempFile = Path.GetTempFileName();
        var imageFilePath = Path.ChangeExtension(tempFile, Path.GetExtension(request.File.FileName));

        // Rename the 0-byte temp file to include the extension
        System.IO.File.Move(tempFile, imageFilePath);

        // 3. Save the uploaded file (RAM) to the temporary path (Disk)
        using (var stream = new FileStream(imageFilePath, FileMode.Create))
        {
            await request.File.CopyToAsync(stream);
        }


#pragma warning disable OPENAI001
        OpenAIFileClient fileClient = _openAIClient.GetOpenAIFileClient();
        AssistantClient assistantClient = _openAIClient.GetAssistantClient();


        OpenAIFile pictureOfAppleFile = fileClient.UploadFile(
        imageFilePath,
        FileUploadPurpose.Vision);

        Assistant assistant = assistantClient.CreateAssistant(
            "gpt-4o",
            new AssistantCreationOptions()
            {
                Instructions = "When asked a question, attempt to answer very concisely. "
                    + "Prefer one-sentence answers whenever feasible."
            });

        AssistantThread thread = assistantClient.CreateThread(new ThreadCreationOptions()
        {
            InitialMessages =
        {
            new ThreadInitializationMessage(
                MessageRole.User,
                [
                    request.Prompt,
                    MessageContent.FromImageFileId(pictureOfAppleFile.Id)
                ]),
        }
        });
#pragma warning restore OPENAI001


        return Ok();
    }

    //[HttpPost]
    //[Route("chat-pdf")]
    //[Consumes("multipart/form-data")]
    //public async Task<IActionResult> DescribePdfPrompt(
    //       [FromForm] DescribeImageRequest request,
    //       CancellationToken ct)
    //{
    //    if (request.File is null)
    //        return BadRequest("Nenhum arquivo enviado");

    //    if (!string.Equals(request.File.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
    //        return BadRequest($"Arquivo não suportado: {request.File.FileName} ({request.File.ContentType})");

    //    byte[] fileBytes;
    //    var base64File = string.Empty;
    //    await using (var ms = new MemoryStream())
    //    {
    //        await request.File.CopyToAsync(ms, ct);
    //        fileBytes = ms.ToArray();
    //        base64File = Convert.ToBase64String(fileBytes);
    //    }

    //    var _anthropicClient = new AnthropicClient();
    //    var messages = new List<Message>()
    //    {
    //        new Message(RoleType.User, new DocumentContent()
    //        {
    //            Source = new DocumentSource()
    //            {
    //                Type = SourceType.base64,
    //                Data = base64File,
    //                MediaType = "application/pdf"
    //            },
    //            CacheControl = new CacheControl()
    //            {
    //                Type = CacheControlType.ephemeral
    //            }
    //        }),
    //        new Message(RoleType.User, request.Prompt),
    //    };

    //    var parameters = new MessageParameters()
    //    {
    //        Messages = messages,
    //        MaxTokens = 1024,
    //        Model = AnthropicModels.Claude45Haiku,
    //        Stream = false,
    //        Temperature = 0m,
    //        PromptCaching = PromptCacheType.FineGrained
    //    };
    //    var response = await _anthropicClient.Messages.GetClaudeMessageAsync(parameters);

    //    // retorna a resposta direta
    //    var chatReponse = response.FirstMessage.Text;

    //    //retorna o objeto completo de resposta
    //    return Ok(response.Message.Content.FirstOrDefault());
    //}


    //[HttpPost]
    //[Route("chat-pdf-scanned")]
    //[Consumes("multipart/form-data")]
    //public async Task<IActionResult> DescribePdfScannedPrompt(
    //   [FromForm] DescribeImageRequest request,
    //    CancellationToken ct)
    //{
    //    if (request.File is null) return BadRequest("Nenhum arquivo enviado");
    //    if (!string.Equals(request.File.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
    //        return BadRequest("Envie um PDF.");

    //    byte[] pdfBytes;
    //    await using (var ms = new MemoryStream())
    //    {
    //        await request.File.CopyToAsync(ms, ct);
    //        pdfBytes = ms.ToArray();
    //    }

    //    // Converte páginas para PNG (ex: 1 a 3 páginas pra teste)
    //    var imageBytes = await _pdfRenderer.SkiaSharpPdfRenderPagesAsPngListAsync(pdfBytes, maxPages: 3, ct);

    //    if (imageBytes.Count == 0)
    //        return BadRequest("Não consegui renderizar páginas desse PDF.");

    //    var chatMessages = new List<ChatMessage>();
    //    ChatOptions option = new()
    //    {
    //        ModelId = AnthropicModels.Claude45Haiku,
    //        MaxOutputTokens = 512,
    //        Temperature = 0f,
    //    };

    //    foreach (var imageByte in imageBytes)
    //    {
    //        var message = new ChatMessage(ChatRole.User,
    //        [
    //            new DataContent(imageByte, "image/png"),
    //            new Microsoft.Extensions.AI.TextContent(request.Prompt),
    //        ]);

    //        chatMessages.Add(message);
    //    }

    //    var response = await _chatClient.GetResponseAsync(chatMessages, option);

    //    return Ok(response.Text);
    //}
}
