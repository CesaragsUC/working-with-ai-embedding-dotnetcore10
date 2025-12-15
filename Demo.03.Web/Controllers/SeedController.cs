using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using OllamaSharp;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Demo.Embedding.Web;

[Route("api/[controller]")]
public class SeedController(Kernel _kernel, OllamaApiClient ollamaApiClient, AppEmbeddingDbContext context) : Controller
{

    [HttpGet]
    [Route("seed")]
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

    [HttpPost]
    [Route("prompt")]
    public async Task<IActionResult> Prompt(string Prompt)
    {
        var embeddingService = ollamaApiClient.AsTextEmbeddingGenerationService();
        var embedding = await embeddingService.GenerateEmbeddingAsync(Prompt);
        var vectorEmbedding = new Vector(embedding.ToArray());

        var sobreClubes = await context.SobreClubes
                                 .AsNoTracking()
                                 .OrderBy(e => e.Embedding.CosineDistance(vectorEmbedding))
                                 .Select(x => new { x.Name, x.Description})
                                 .Take(3)//pega os 3 mais proximos. se quiser dar pra deixar 1 para pegar somente o mais proximo
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
}
