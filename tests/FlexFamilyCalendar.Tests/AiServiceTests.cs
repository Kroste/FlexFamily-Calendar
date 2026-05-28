using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using Xunit;

namespace FlexFamilyCalendar.Tests;

[Collection("SecretService")]
public class AiServiceTests
{
    private sealed class FakeProvider : IAiProvider
    {
        public FakeProvider(string name) => Name = name;
        public string Name { get; }
        public string? AppliedKey;
        public string? AppliedModel;
        public bool RequiresApiKey => true;
        public bool IsConfigured => !string.IsNullOrEmpty(AppliedKey);
        public void SetApiKey(string key) => AppliedKey = key;
        public void SetModel(string model) => AppliedModel = model;
        public Task<string> CompleteAsync(string prompt, CancellationToken ct = default) => Task.FromResult("OK");
    }

    public AiServiceTests()
        => SecretService.Initialize(Path.Combine(Path.GetTempPath(), "ffc_ai_" + Guid.NewGuid().ToString("N")));

    [Fact]
    public void ApplySettings_DecryptsKey_SetsModel_AndActiveProvider()
    {
        var fake = new FakeProvider("TestAI");
        var svc = new AiService(new IAiProvider[] { fake });
        var settings = new AppSettings
        {
            ActiveAiProvider = "TestAI",
            AiModel = "model-x",
            EncryptedApiKeys = { ["TestAI"] = SecretService.Encrypt("geheim-123") }
        };

        svc.ApplySettings(settings);

        Assert.Equal("geheim-123", fake.AppliedKey);
        Assert.Equal("model-x", fake.AppliedModel);
        Assert.Equal("TestAI", svc.ActiveProvider?.Name);
    }

    [Fact]
    public async Task SuggestAsync_NoActiveProvider_ReturnsNull()
    {
        var svc = new AiService(new IAiProvider[] { new FakeProvider("X") });
        Assert.Null(await svc.SuggestAsync("hi"));
    }

    [Fact]
    public async Task TestAsync_TrueWhenConfiguredProviderAnswers()
    {
        var fake = new FakeProvider("X");
        fake.SetApiKey("k");
        var svc = new AiService(new IAiProvider[] { fake });
        svc.SetActiveProvider("X");
        Assert.True(await svc.TestAsync());
    }

    [Fact]
    public void ApplySettings_UnknownProviderKey_Ignored()
    {
        var fake = new FakeProvider("Known");
        var svc = new AiService(new IAiProvider[] { fake });
        var settings = new AppSettings
        {
            EncryptedApiKeys = { ["Unknown"] = SecretService.Encrypt("x") }
        };
        svc.ApplySettings(settings);   // darf nicht werfen
        Assert.Null(fake.AppliedKey);
    }
}
