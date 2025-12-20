using DocumentFormat.OpenXml.Wordprocessing;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Embeddings;
using OllamaSharp;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Demo.Embedding.Web;

[Route("api/[controller]")]
public class OllamaClientController(
    Kernel _kernel,
    OllamaApiClient ollamaApiClient,
    AppEmbeddingDbContext context,
    Client geminiClient,
    UnifiedDocumentService documentService,
    GoogleAIGeminiChatCompletionService googleAIGeminiChat) : Controller
{

    [HttpPost]
    [Route("ollama-futebol-seed")]
    public async Task<IActionResult> Seed()
    {
        var embeddingService = ollamaApiClient.AsTextEmbeddingGenerationService();

        var clubes = await context.Clubes.AsNoTracking().ToListAsync();

        var sobreClubesList = new List<SobreClubes>();

        foreach (var item in clubes)
        {
            var embedding = await embeddingService.GenerateEmbeddingAsync(item.Description);

            var sobreClube = new SobreClubes
            {
                Id = Guid.NewGuid(),
                ClubeId = item.Id,
                Name = item.Name,
                Country = item.Country,
                State = item.State,
                Description = item.Description,
                Embedding = new Vector(embedding)
            };

            sobreClubesList.Add(sobreClube);
        }

        await context.SobreClubes.AddRangeAsync(sobreClubesList);
        await context.SaveChangesAsync();

        return Ok(new { message = "Seed completed successfully" });
    }


    [HttpPost]
    [Route("add-futebol-clube")]
    public async Task<IActionResult> Add(ClubeAddDto clubeAdd)
    {
        var clube = new Clubes
        {
            Name = clubeAdd.Name,
            Country = clubeAdd.Country,
            State = clubeAdd.State,
            Description = clubeAdd.Description
        };

        await context.Clubes.AddAsync(clube);

        var embeddingService = ollamaApiClient.AsTextEmbeddingGenerationService();
        var embedding = await embeddingService.GenerateEmbeddingAsync(clube.Description);

        var sobreClube = new SobreClubes
        {
            Id = Guid.NewGuid(),
            Name = clube.Name,
            Country = clube.Country,
            State = clube.State,
            Description = clube.Description,
            Embedding = new Vector(embedding)
        };

        await context.SobreClubes.AddAsync(sobreClube);

        await context.SaveChangesAsync();

        return Ok(new { message = "Clube added successfully" });
    }

    [HttpGet]
    [Route("ollama-sobre-futebol-clube")]
    public async Task<IActionResult> ClubePrompt(string prompt)
    {
        var embeddingService = ollamaApiClient.AsTextEmbeddingGenerationService();
        var embedding = await embeddingService.GenerateEmbeddingAsync(prompt);
        var vectorEmbedding = new Vector(embedding.ToArray());

        var sobreClubes = await context.SobreClubes
                                 .AsNoTracking()
                                 .OrderBy(e => e.Embedding.CosineDistance(vectorEmbedding))
                                 .Select(x => new { x.Id, x.Name, x.Country, x.State,  x.Description })
                                 .Take(1)//pega os 3 mais proximos. se quiser dar pra deixar 1 para pegar somente o mais proximo
                                 .ToListAsync();

        if (!sobreClubes.Any())
        {
            return Ok(new
            {
                message = "Nenhum clube encontrado para essa busca",
                results = new List<object>()
            });
        }

        return Ok(sobreClubes);
    }

    [HttpPost]
    [Route("ollama-product-seed")]
    public async Task<IActionResult> ProductPromptOllama()
    {
        var embeddingService = ollamaApiClient.AsTextEmbeddingGenerationService();
        var products = await context.Products.AsNoTracking().ToListAsync();

        var productsRecomendationList = new List<ProductsRecomendation>();

        foreach (var item in products)
        {
            var embedding = await embeddingService.GenerateEmbeddingAsync(item.Description);

            var recomendation = new ProductsRecomendation
            {
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                Category = item.Category,
                EmbeddingLLM = new Vector(embedding)
            };

            productsRecomendationList.Add(recomendation);
        }

        await context.ProductsRecomendation.AddRangeAsync(productsRecomendationList);
        await context.SaveChangesAsync();

        return Ok(new { message = "Seed completed successfully" });
    }

    [HttpGet]
    [Route("ollama-product")]
    public async Task<IActionResult> ProductPrompt(string prompt)
    {
        var embeddingService = ollamaApiClient.AsTextEmbeddingGenerationService();
        var embedding = await embeddingService.GenerateEmbeddingAsync(prompt);
        var vectorEmbedding = new Vector(embedding.ToArray());

        var products = await context.ProductsRecomendation
                                 .AsNoTracking()
                                 .OrderBy(e => e.EmbeddingLLM.CosineDistance(vectorEmbedding))
                                 .Select(x => new {x.Id, x.Name, x.Category,x.Price, x.Description })
                                 .Take(1)//pega os 3 mais proximos. se quiser dar pra deixar 1 para pegar somente o mais proximo
                                 .ToListAsync();

        if (!products.Any())
        {
            return Ok(new
            {
                message = "Nenhum product encontrado para essa busca",
                results = new List<object>()
            });
        }

        return Ok(products);
    }

}