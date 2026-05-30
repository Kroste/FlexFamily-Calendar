using System.Text;

namespace FlexFamilyCalendar.Services.AI;

public enum ChatRole { User, Assistant }

public record ChatMessage(ChatRole Role, string Text);

/// <summary>
/// Multi-Turn-Chat über den bestehenden <see cref="AiService"/>. Der Provider-Vertrag
/// (<see cref="IAiProvider.CompleteAsync"/>) ist single-shot — wir verketten daher
/// Kontext-Block, Notizen, bisherigen Verlauf und die neue User-Nachricht zu einem
/// einzigen Prompt. Damit bleibt jeder Provider (Anthropic/OpenAI/Gemini/…)
/// gleichermaßen nutzbar, ohne sein API erweitern zu müssen.
/// </summary>
public class AiChatService
{
    private readonly AiService _ai;
    public AiChatService(AiService ai) => _ai = ai;

    /// <summary>
    /// Sendet die User-Nachricht zusammen mit dem System-Kontext und allen vorherigen
    /// Turns an die aktuell aktive KI. Gibt die Antwort zurück oder <c>null</c>, falls
    /// kein Provider/Schlüssel verfügbar ist.
    /// </summary>
    public Task<string?> AskAsync(string contextBlock, IReadOnlyList<ChatMessage> history,
        string userMessage, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(contextBlock, history, userMessage);
        return _ai.SuggestAsync(prompt, ct);
    }

    /// <summary>
    /// Reine Prompt-Konstruktion — testbar ohne Provider. Der Block ist:
    /// [System-Kontext] → [bisheriger Verlauf] → [neue User-Nachricht].
    /// </summary>
    public static string BuildPrompt(string contextBlock, IReadOnlyList<ChatMessage> history, string userMessage)
    {
        var sb = new StringBuilder();
        sb.AppendLine(contextBlock);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        foreach (var m in history)
        {
            sb.AppendLine(m.Role == ChatRole.User ? "Benutzer:" : "Assistent:");
            sb.AppendLine(m.Text);
            sb.AppendLine();
        }
        sb.AppendLine("Benutzer:");
        sb.AppendLine(userMessage);
        sb.AppendLine();
        sb.Append("Assistent:");
        return sb.ToString();
    }
}
