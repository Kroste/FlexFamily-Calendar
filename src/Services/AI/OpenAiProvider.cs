namespace FlexFamilyCalendar.Services.AI;

public class OpenAiProvider : OpenAiCompatibleProvider
{
    public OpenAiProvider(HttpClient? http = null) : base(http) { }

    public override string Name => "ChatGPT";
    protected override string DefaultModel => "gpt-4o-mini";
    protected override string Endpoint => "https://api.openai.com/v1/chat/completions";
}
