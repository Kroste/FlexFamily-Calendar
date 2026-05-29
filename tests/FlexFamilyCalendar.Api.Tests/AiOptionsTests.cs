using FlexFamilyCalendar.Api.Ai;

namespace FlexFamilyCalendar.Api.Tests;

public class AiOptionsTests
{
    [Fact]
    public void Empty_HasKey_FalseForAll()
    {
        var o = new AiOptions();
        Assert.False(o.HasKey("anthropic"));
        Assert.False(o.HasKey("openai"));
        Assert.False(o.HasKey("chatgpt"));
        Assert.False(o.HasKey("gemini"));
        Assert.False(o.HasKey("perplexity"));
    }

    [Fact]
    public void AnthropicKey_HasKey_TrueForAnthropic_Only()
    {
        var o = new AiOptions { AnthropicKey = "sk-ant-foo" };
        Assert.True(o.HasKey("anthropic"));
        Assert.True(o.HasKey("ANTHROPIC"));   // case-insensitive
        Assert.False(o.HasKey("openai"));
    }

    [Fact]
    public void OpenAiKey_HasKey_TrueForOpenAi_AndChatGptAlias()
    {
        var o = new AiOptions { OpenAiKey = "sk-foo" };
        Assert.True(o.HasKey("openai"));
        Assert.True(o.HasKey("chatgpt"));     // Client nennt es ChatGPT
        Assert.False(o.HasKey("gemini"));
    }

    [Fact]
    public void Whitespace_NotConsideredSet()
        => Assert.False(new AiOptions { GeminiKey = "   " }.HasKey("gemini"));

    [Fact]
    public void UnknownProvider_False()
        => Assert.False(new AiOptions { AnthropicKey = "x" }.HasKey("nonsense"));
}
