using Microsoft.SemanticKernel;

namespace Demo.Embedding.Web.Utils;

public class FunctionFilter : IAutoFunctionInvocationFilter
{
    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
    {
        Console.WriteLine($"{nameof(FunctionFilter)}.FunctionInvoking - {context.Function.PluginName}.{context.Function.Name}");
        await next(context);
        Console.WriteLine($"{nameof(FunctionFilter)}.FunctionInvoked - {context.Function.PluginName}.{context.Function.Name}");
    }
}