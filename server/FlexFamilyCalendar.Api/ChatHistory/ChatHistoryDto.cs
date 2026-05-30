namespace FlexFamilyCalendar.Api.ChatHistory;

public record ChatHistoryDto(Guid Id, string Role, string Text, DateTime CreatedAtUtc);
