using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace NileChain.AI.Sbg;

/// <summary>
/// SK chat completion backed by SBG /student/chat (no native tool/function calling).
/// </summary>
public sealed class SbgChatCompletionService : IChatCompletionService
{
    private readonly SbgStudentChatClient _client;

    public SbgChatCompletionService(SbgStudentChatClient client)
    {
        _client = client;
    }

    public IReadOnlyDictionary<string, object?> Attributes { get; } =
        new Dictionary<string, object?>();


    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        string? system = null;
        var messages = new List<SbgChatMessage>();

        foreach (var msg in chatHistory)
        {
            var text = msg.Content ?? string.Empty;
            if (msg.Role == AuthorRole.System)
            {
                system = string.IsNullOrEmpty(system) ? text : system + "\n" + text;
                continue;
            }

            var role = msg.Role == AuthorRole.Assistant ? "assistant" : "user";
            messages.Add(new SbgChatMessage { Role = role, Content = text });
        }

        if (messages.Count == 0)
            messages.Add(new SbgChatMessage { Role = "user", Content = "Continue." });

        var reply = await _client.ChatAsync(system, messages, cancellationToken: cancellationToken);
        return [new ChatMessageContent(AuthorRole.Assistant, reply)];
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var full = await GetChatMessageContentsAsync(
            chatHistory, executionSettings, kernel, cancellationToken);
        var text = full.FirstOrDefault()?.Content ?? string.Empty;
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, text);
    }
}
