using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Demo.Embedding.Web;

public interface ITextProcessorKf
{
    [KernelFunction("to_upper")]
    [Description("Converts text to uppercase.")]
    string ToUpper(string input);
}

public class TextProcessorKf : ITextProcessorKf
{
    [KernelFunction]
    [Description("Converts text to uppercase.")]
    public string ToUpper(string input) => input.ToUpper();
}