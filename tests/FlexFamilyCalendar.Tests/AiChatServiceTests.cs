using FlexFamilyCalendar.Services.AI;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class AiChatServiceTests
{
    [Fact]
    public void BuildPrompt_StartsWithContextBlock()
    {
        var p = AiChatService.BuildPrompt("## Kontext\nLars arbeitet 40h", Array.Empty<ChatMessage>(), "Hi");
        Assert.StartsWith("## Kontext", p);
        Assert.Contains("Lars arbeitet 40h", p);
    }

    [Fact]
    public void BuildPrompt_AppendsUserMessage_AndAssistantHook()
    {
        var p = AiChatService.BuildPrompt("ctx", Array.Empty<ChatMessage>(), "Plane Mo–Fr");
        Assert.Contains("Benutzer:\nPlane Mo–Fr", p);
        Assert.EndsWith("Assistent:", p);
    }

    [Fact]
    public void BuildPrompt_AppliesHistory_InOrder()
    {
        var history = new[] {
            new ChatMessage(ChatRole.User, "Wer arbeitet morgen?"),
            new ChatMessage(ChatRole.Assistant, "Sneha und Lars."),
        };
        var p = AiChatService.BuildPrompt("ctx", history, "Und übermorgen?");

        var benutzer1 = p.IndexOf("Benutzer:\nWer arbeitet morgen?");
        var assist1 = p.IndexOf("Assistent:\nSneha und Lars.");
        var benutzer2 = p.IndexOf("Benutzer:\nUnd übermorgen?");

        Assert.True(benutzer1 >= 0);
        Assert.True(assist1 > benutzer1);
        Assert.True(benutzer2 > assist1);
    }

    [Fact]
    public void BuildPrompt_HasSeparator_BetweenContextAndConversation()
    {
        var p = AiChatService.BuildPrompt("ctx", Array.Empty<ChatMessage>(), "Hi");
        Assert.Contains("---", p);
    }
}
