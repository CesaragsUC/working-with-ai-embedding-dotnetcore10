using System.ComponentModel.DataAnnotations;

namespace Demo.Embedding.Web;

public class ChatRequest
{
    [Required]
    [StringLength(10000, MinimumLength = 1)]
    public string Prompt { get; set; } = string.Empty;
}