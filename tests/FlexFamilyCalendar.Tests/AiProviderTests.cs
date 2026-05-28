using System.Net;
using FlexFamilyCalendar.Services.AI;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class AiProviderTests
{
    /// <summary>Fake-Handler, der eine feste JSON-Antwort liefert und die letzte Anfrage festhält.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public StubHandler(string json) => _json = json;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            if (request.Content != null) LastBody = await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json)
            };
        }
    }

    private static HttpClient Client(string json) => new(new StubHandler(json));

    [Fact]
    public async Task OpenAi_ParsesChoiceContent()
    {
        var http = Client("{\"choices\":[{\"message\":{\"content\":\"Hallo\"}}]}");
        var p = new OpenAiProvider(http);
        p.SetApiKey("k");
        Assert.True(p.RequiresApiKey);
        Assert.True(p.IsConfigured);
        Assert.Equal("Hallo", await p.CompleteAsync("hi"));
    }

    [Fact]
    public async Task Perplexity_ParsesChoiceContent()
    {
        var p = new PerplexityProvider(Client("{\"choices\":[{\"message\":{\"content\":\"Pong\"}}]}"));
        p.SetApiKey("k");
        Assert.Equal("Pong", await p.CompleteAsync("hi"));
    }

    [Fact]
    public async Task Anthropic_ParsesContentText()
    {
        var p = new AnthropicProvider(Client("{\"content\":[{\"type\":\"text\",\"text\":\"Servus\"}]}"));
        p.SetApiKey("k");
        Assert.Equal("Servus", await p.CompleteAsync("hi"));
    }

    [Fact]
    public async Task Gemini_ParsesCandidateText()
    {
        var p = new GeminiProvider(Client("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Moin\"}]}}]}"));
        p.SetApiKey("k");
        Assert.Equal("Moin", await p.CompleteAsync("hi"));
    }

    [Fact]
    public async Task Llama_ParsesResponse_NoKeyNeeded()
    {
        var p = new LlamaProvider(Client("{\"response\":\"Hi vom lokalen Modell\"}"));
        Assert.False(p.RequiresApiKey);
        Assert.True(p.IsConfigured);   // lokal, kein Schlüssel nötig
        Assert.Equal("Hi vom lokalen Modell", await p.CompleteAsync("hi"));
    }

    [Fact]
    public async Task NotConfigured_WhenNoKey()
    {
        var p = new OpenAiProvider(Client("{}"));
        Assert.False(p.IsConfigured);
    }

    [Fact]
    public async Task OpenAi_UsesModelOverride_InRequestBody()
    {
        var handler = new StubHandler("{\"choices\":[{\"message\":{\"content\":\"x\"}}]}");
        var p = new OpenAiProvider(new HttpClient(handler));
        p.SetApiKey("k");
        p.SetModel("gpt-4o");
        await p.CompleteAsync("hi");
        Assert.Contains("\"model\":\"gpt-4o\"", handler.LastBody);
        Assert.Equal("Bearer k", handler.LastRequest!.Headers.GetValues("Authorization").Single());
    }

    [Fact]
    public async Task HttpError_Throws()
    {
        var handler = new ThrowingHandler();
        var p = new OpenAiProvider(new HttpClient(handler));
        p.SetApiKey("k");
        await Assert.ThrowsAsync<HttpRequestException>(() => p.CompleteAsync("hi"));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"bad key\"}")
            });
    }
}
