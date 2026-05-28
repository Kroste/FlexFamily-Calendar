namespace FlexFamilyCalendar.Services.AI;

public class PerplexityProvider : OpenAiCompatibleProvider
{
    public PerplexityProvider(HttpClient? http = null) : base(http) { }

    public override string Name => "Perplexity";
    protected override string DefaultModel => "sonar";
    protected override string Endpoint => "https://api.perplexity.ai/chat/completions";
}
