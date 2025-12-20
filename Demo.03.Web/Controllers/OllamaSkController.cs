using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using System.Diagnostics;

namespace Demo.Embedding.Web.Controllers;

public class OllamaSkController : Controller
{
    private readonly Stopwatch _timer = new();
    private readonly Kernel _kernel;
    private readonly AppEmbeddingDbContext _context;
    private readonly UnifiedDocumentService _documentService;
    private readonly IPdfPageRenderer _pdfRenderer;

    public OllamaSkController(
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
    [Route("chat-kernelfunction-auto")]
    public async Task<IActionResult> AutoInvocation([FromQuery] string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            return BadRequest(new { error = "Prompt is null or empty" });
        }

        // 2. Configurar para AI decidir quando usar as funções
        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() 
        };
        var result = await _kernel.InvokePromptAsync(prompt, new KernelArguments(settings));

        return Ok(new { response = result.ToString() });
    }
}
