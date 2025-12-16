using Google.GenAI.Types;
using Google.GenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using OllamaSharp;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Demo.Embedding.Web;

[Route("api/[controller]")]
public class SeedController(
    OllamaApiClient ollamaApiClient,
    AppEmbeddingDbContext context,
    Client geminiClient,
    UnifiedDocumentService documentService) : Controller
{

    [HttpPost]
    [Route("ollama-seed")]
    public async Task<IActionResult> Seed()
    {
        var embeddingService = ollamaApiClient.AsTextEmbeddingGenerationService();

        //OPCAO 1: Usar o Docling para ler documentos e gerar embeddings a partir deles.
        //Docling é uma open source para ler arquivos como PDF, Word, Excel, PowerPoint, TXT, entre outros, e extrair informações e gerar embeddings para esses documentos.

        var clubes = await context.Clubes.AsNoTracking().ToListAsync();

        var sobreClubesList = new List<SobreClubes>();

        foreach (var item in clubes)
        {
            var embedding = await embeddingService.GenerateEmbeddingAsync(item.Description);

            var sobreClube = new SobreClubes
            {
                Id = item.Id,
                Name = item.Name,
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
    [Route("add-clube")]
    public async Task<IActionResult> Add(ClubeAddDto clubeAdd)
    {
        var clube = new Clubes
        {
            Name = clubeAdd.Name,
            Description = clubeAdd.Description
        };

        await context.Clubes.AddAsync(clube);

        var embeddingService = ollamaApiClient.AsTextEmbeddingGenerationService();
        var embedding = await embeddingService.GenerateEmbeddingAsync(clube.Description);

        var sobreClube = new SobreClubes
        {
            Id = 6, // Defina o Id conforme necessário
            Name = clube.Name,
            Description = clube.Description,
            Embedding = new Vector(embedding)
        };

        await context.SobreClubes.AddAsync(sobreClube);

        await context.SaveChangesAsync();

        return Ok(new { message = "Clube added successfully" });
    }

    [HttpGet]
    [Route("ollama-sobre-clube")]
    public async Task<IActionResult> ClubePrompt(string prompt)
    {
        var embeddingService = ollamaApiClient.AsTextEmbeddingGenerationService();
        var embedding = await embeddingService.GenerateEmbeddingAsync(prompt);
        var vectorEmbedding = new Vector(embedding.ToArray());

        var sobreClubes = await context.SobreClubes
                                 .AsNoTracking()
                                 .OrderBy(e => e.Embedding.CosineDistance(vectorEmbedding))
                                 .Select(x => new { x.Name, x.Description })
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

    //[HttpGet]
    //[Route("ollama-product")]
    //public async Task<IActionResult> ProductPrompt(string prompt)
    //{
    //    var embeddingService = ollamaApiClient.AsTextEmbeddingGenerationService();
    //    var embedding = await embeddingService.GenerateEmbeddingAsync(prompt);
    //    var vectorEmbedding = new Vector(embedding.ToArray());

    //    var products = await context.ProductsRecomendation
    //                             .AsNoTracking()
    //                             .OrderBy(e => e.Embedding.CosineDistance(vectorEmbedding))
    //                             .Select(x => new { x.Name, x.Description })
    //                             .Take(1)//pega os 3 mais proximos. se quiser dar pra deixar 1 para pegar somente o mais proximo
    //                             .ToListAsync();

    //    if (!products.Any())
    //    {
    //        return Ok(new
    //        {
    //            message = "Nenhum product encontrado para essa busca",
    //            results = new List<object>()
    //        });
    //    }

    //    return Ok(products);
    //}

    #region Gemini Example

    [HttpPost]
    [Route("gemini-product-seed")]
    public async Task<IActionResult> GeminiSeed()
    {
        var products = await context.Products.AsNoTracking().ToListAsync();

        var productsRecomendations = new List<ProductsRecomendation>();

        foreach (var item in products)
        {

            var response = await geminiClient.Models.EmbedContentAsync(
                                   model: "text-embedding-004",
                                   contents: item.Description
                                 );

            var embedding = response.Embeddings[0].Values.ToArray();

            var embeddingResult = new ReadOnlyMemory<float>(embedding.Select(x => (float)x).ToArray());

            var product = new ProductsRecomendation
            {
                Id = item.Id,
                Name = item.Name,
                Category = item.Category,
                Price = item.Price,
                Description = item.Description,
                Embedding = new Vector(embeddingResult)
            };

            productsRecomendations.Add(product);
        }

        //await context.ProductsRecomendation.AddRangeAsync(productsRecomendations);
        //await context.SaveChangesAsync();

        await context.BulkInsertAsync(productsRecomendations);

        return Ok(new { message = "Seed completed successfully" });
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

    #endregion
}

public static class VectorHelper
{
    /// <summary>
    /// Converte qualquer coleção de float para Vector do pgvector
    /// </summary>
    public static Vector ToVector(this IEnumerable<float> values)
    {
        var array = values as float[] ?? values.ToArray();
        return new Vector(new ReadOnlyMemory<float>(array));
    }

    /// <summary>
    /// Converte embedding do Gemini diretamente para Vector
    /// </summary>
    public static Vector ToVector(this IList<float> values)
    {
        var array = values as float[] ?? values.ToArray();
        return new Vector(new ReadOnlyMemory<float>(array));
    }
}