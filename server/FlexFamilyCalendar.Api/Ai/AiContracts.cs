namespace FlexFamilyCalendar.Api.Ai;

public record AiCompleteRequest(string Provider, string Prompt, string? Model = null);

public record AiCompleteResponse(string Text);
