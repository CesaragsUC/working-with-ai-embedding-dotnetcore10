using Azure;
using Azure.AI.OpenAI;
using Demo.Embedding.Web.Configurations.ConfigsModel;
using Demo.Embedding.Web.Services;
using Demo.Embedding.Web.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Embeddings;
using OpenAI.Images;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Serilog;
using System.ClientModel;

namespace Demo.Embedding.Web.Controllers;


[Route("api/[controller]")]
public class AzureAISkController : Controller
{
    private readonly IAzureSoraService _azureSoraService;
    private readonly AudioClient  _audioClient;
    private readonly ChatClient _chatClient;
    private readonly ImageClient _imageClient;
    private readonly EmbeddingClient _embeddingClient;
    private readonly AppEmbeddingDbContext _context;
    private readonly AzureAIOptions _option;

    public AzureAISkController(
        IAzureSoraService azureSoraService,
        AudioClient audioClient,
        ChatClient chatClient,
        ImageClient imageClient,
        EmbeddingClient embeddingClient,
        AppEmbeddingDbContext context,
        IOptions<AzureAIOptions> option)
    {
        _azureSoraService = azureSoraService;
        _audioClient = audioClient;
        _chatClient = chatClient;
        _imageClient = imageClient;
        _embeddingClient = embeddingClient;
        _context = context;
        _option = option.Value;
    }

    [HttpGet]
    [Route("chat")]
    public async Task<IActionResult> Chat(string prompt)
    {
        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 500, //LIMITE DE TOKENS DE SAÍDA. Acima de 500 (respostas longas)
            Temperature = 1f     //Respostas mais curtas e objetivas. quanto maior o valor, mais criativa a resposta. Porem pode aumentar o custo.
        };

        List<ChatMessage> chatMessages =
        [
             new UserChatMessage(prompt),
        ];

        ChatCompletion completion = await _chatClient.CompleteChatAsync(chatMessages, options);

        return Ok(new { response = completion.Content[0].Text });
    }

    [HttpPost]
    [Route("sora")]
    public async Task<IActionResult> Sora(string prompt)
    {
        var token = Environment.GetEnvironmentVariable("AZURE_SORA_API_KEY");
        var authHeaderValue = $"Bearer {token}";

        var body = new
        {
            prompt = prompt,
            n_variants = "1",
            n_seconds = "5",
            height = "1080",
            width = "1080",
            model = _option.VideoModel
        };
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", authHeaderValue);

            var response = await _azureSoraService.VideoGenerationRequest(_option.VideoModel, body, client).ConfigureAwait(false);
            if (response.generations.Count > 0)
            {
                string generationId = response.generations[0].id;
                await _azureSoraService.SaveVideoContent(_option.VideoModelEndpoint, _option.VideoModel, generationId, client, "output.mp4").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Video generation request failed: {ex.Message}");
        }

        return Ok();
    }


    [HttpPost]
    [Route("dall-e-3")]
    public async Task<IActionResult> DallE(string prompt)
    {
        ClientResult<GeneratedImage> imageResult = await _imageClient.GenerateImageAsync(prompt, new()
        {
            Quality = GeneratedImageQuality.Standard,
            Size = GeneratedImageSize.W1024xH1024,
            Style = GeneratedImageStyle.Vivid,
            ResponseFormat = GeneratedImageFormat.Uri
        });

        GeneratedImage image = imageResult.Value;
        Console.WriteLine($"Image URL: {image.ImageUri}");

        return Ok();
    }

    [HttpPost]
    [Route("wisper")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Wisper([FromForm] ChatRequest request)
    {
        // 2. Create a temporary file path with the correct extension
        // The SDK needs a real path, and sometimes checks the extension (e.g. .mp3, .wav)
        var tempFile = Path.GetTempFileName();
        var audioFilePath = Path.ChangeExtension(tempFile, Path.GetExtension(request.File.FileName));
        var fileSize = FileSizeHelper.GetReadableFileSize(request.File.Length);


        Log.Information("Receiving audio file: {FileName} ({FileSize})",
            request.File.FileName,
            fileSize);

        // Rename the 0-byte temp file to include the extension
        System.IO.File.Move(tempFile, audioFilePath);

        // 3. Save the uploaded file (RAM) to the temporary path (Disk)
        using (var stream = new FileStream(audioFilePath, FileMode.Create))
        {
            await request.File.CopyToAsync(stream);
        }

        string key = Environment.GetEnvironmentVariable("AZURE_WISPER_API_KEY");
        AzureOpenAIClient openAIClient = new AzureOpenAIClient(_option.AudioModelEndpoint, new AzureKeyCredential(key));
        var audioClient = openAIClient.GetAudioClient(_option.AudioModel);

        AudioTranscriptionOptions options = new()
        {
            ResponseFormat = AudioTranscriptionFormat.Verbose,
            TimestampGranularities = AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment,
        };

        var transcription = await audioClient.TranscribeAudioAsync(audioFilePath, options);

        return Ok(new { response = transcription.Value.Text });
    }

    [HttpPost]
    [Route("embedding")]
    public async Task<IActionResult> Embedding(string prompt)
    {
        OpenAIEmbedding embedding = await _embeddingClient.GenerateEmbeddingAsync(prompt);
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
}
