using Demo.Embedding.Web;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Demo.Embedding.Web;

public class ChatService(IChatClient _chatClient,
    IDistributedCache cache,
    ILogger<ChatService> _logger)
{
    private readonly Dictionary<string, List<ChatMessage>> _conversations = new();

    // Tempo de expiração do histórico (24 horas)
    private readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
        SlidingExpiration = TimeSpan.FromHours(1)
    };


    /// <summary>
    ///  Versao com userId para manter o contexto da conversa por usuário
    ///  Mantem o contexto entre chamadas para o mesmo usuário.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="prompt"></param>
    /// <returns></returns>
    public async Task<string> ChatUserAsync(string chatId, string prompt)
    {
        // Cria ou recupera a conversa
        var cacheKey = $"chat:history:{chatId}";
        var messages = await GetConversationHistory(cacheKey);

        // Adiciona a pergunta do usuário
        messages.Add(new ChatMessageDto
        {
            Role = "user",
            Content = prompt
        });

        // Converte para o formato da API
        var apiMessages = messages.Select(m => new ChatMessage(
            m.Role == "system" ? ChatRole.System :
            m.Role == "user" ? ChatRole.User :
            ChatRole.Assistant,
            m.Content
        )).ToList();

        var chatOptions = new ChatOptions
        {
            Temperature = 0.0f,
            ResponseFormat = ChatResponseFormat.Text
        };

        // Pega resposta da IA
        var response = await _chatClient.GetResponseAsync(apiMessages, chatOptions);

        // Adiciona resposta ao histórico
        messages.Add(new ChatMessageDto
        {
            Role = "assistant",
            Content = response.Text
        });

        // Salva histórico atualizado no cache
        await SaveConversationHistory(cacheKey, messages);

        return response.Text;
    }

    /// <summary>
    ///  Essa versao sem userId, para conversas simples sem um usuário específico.
    ///  Mantem o contexto apenas durante a execução do método.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="prompt"></param>
    /// <returns></returns>
    public async Task<string> ChatAsync(string prompt)
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, "Você é um especialista em kubernetes. Responda de forma concisa."),
            new ChatMessage(ChatRole.System, "Contexto adicional: O usuário trabalha com Azure Kubernetes Service (AKS) e usa .NET 8."),
            new ChatMessage(ChatRole.User, prompt)
        };

        var chatOptions = new ChatOptions
        {
            Temperature = 0.0f,
            ResponseFormat = ChatResponseFormat.Text
        };

        // Pega a resposta da IA
        var response = await _chatClient.GetResponseAsync(messages, chatOptions);

        return response.Text;
    }

    private async Task<List<ChatMessageDto>> GetConversationHistory(string cacheKey)
    {
        var cachedData = await cache.GetStringAsync(cacheKey);

        if (string.IsNullOrEmpty(cachedData))
        {
            // Primeira conversa - cria com mensagem de sistema
            return new List<ChatMessageDto>
            {
                new ChatMessageDto
                {
                    Role = "system",
                    Content = "Você é um especialista em kubernetes. Responda de forma concisa."
                },
                new ChatMessageDto
                {
                    Role = "system",
                    Content = "Contexto adicional: O usuário trabalha com Azure Kubernetes Service (AKS) e usa .NET 8."
                }
            };
        }

        try
        {
            return JsonSerializer.Deserialize<List<ChatMessageDto>>(cachedData)
                   ?? new List<ChatMessageDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deserializar histórico do cache");
            return new List<ChatMessageDto>();
        }
    }

    private async Task SaveConversationHistory(string cacheKey, List<ChatMessageDto> messages)
    {
        // Limita o histórico (últimas 20 mensagens + system message)
        var systemMessages = messages.Where(m => m.Role == "system").ToList();
        var conversationMessages = messages.Where(m => m.Role != "system").TakeLast(20).ToList();

        var limitedMessages = systemMessages.Concat(conversationMessages).ToList();

        var json = JsonSerializer.Serialize(limitedMessages);
        await cache.SetStringAsync(cacheKey, json, _cacheOptions);
    }

    public async Task ClearHistory(string chatId)
    {
        var cacheKey = $"chat:history:{chatId}";
        await cache.RemoveAsync(cacheKey);
    }
}
