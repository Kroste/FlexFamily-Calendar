using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.AI;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class PlannerSuggestionParserTests
{
    [Fact]
    public void Extract_Add_FromJsonBlock_ReturnsSuggestion()
    {
        var text = """
            Ich schlage vor, dass Lars die Frühschicht übernimmt:
            ```json
            {"action":"add","date":"2026-06-01","userId":"u1","type":"Work","start":"06:00","end":"14:00","title":"Frühschicht"}
            ```
            """;
        var s = Assert.Single(PlannerSuggestionParser.Extract(text));
        Assert.Equal(SuggestionAction.Add, s.Action);
        Assert.Equal(new DateOnly(2026, 6, 1), s.Date);
        Assert.Equal("u1", s.UserId);
        Assert.Equal(EntryType.Work, s.Type);
        Assert.Equal(TimeSpan.FromHours(6), s.Start);
        Assert.Equal(TimeSpan.FromHours(14), s.End);
        Assert.Equal("Frühschicht", s.Title);
    }

    [Fact]
    public void Extract_Update_RequiresEntryIdAndAtLeastOneField()
    {
        var ok = "```json\n{\"action\":\"update\",\"date\":\"2026-06-01\",\"entryId\":\"e1\",\"start\":\"07:00\",\"end\":\"15:00\"}\n```";
        var s = Assert.Single(PlannerSuggestionParser.Extract(ok));
        Assert.Equal(SuggestionAction.Update, s.Action);
        Assert.Equal("e1", s.EntryId);
        Assert.Equal(TimeSpan.FromHours(7), s.Start);

        var missingId = "```json\n{\"action\":\"update\",\"date\":\"2026-06-01\",\"start\":\"07:00\"}\n```";
        Assert.Empty(PlannerSuggestionParser.Extract(missingId));

        var missingChanges = "```json\n{\"action\":\"update\",\"date\":\"2026-06-01\",\"entryId\":\"e1\"}\n```";
        Assert.Empty(PlannerSuggestionParser.Extract(missingChanges));
    }

    [Fact]
    public void Extract_Delete_RequiresOnlyEntryId()
    {
        var ok = "```json\n{\"action\":\"delete\",\"date\":\"2026-06-01\",\"entryId\":\"e1\"}\n```";
        var s = Assert.Single(PlannerSuggestionParser.Extract(ok));
        Assert.Equal(SuggestionAction.Delete, s.Action);
        Assert.Equal("e1", s.EntryId);

        var missingId = "```json\n{\"action\":\"delete\",\"date\":\"2026-06-01\"}\n```";
        Assert.Empty(PlannerSuggestionParser.Extract(missingId));
    }

    [Fact]
    public void Extract_MultipleBlocks_AllExtracted()
    {
        var text = """
            Option A:
            ```json
            {"action":"add","date":"2026-06-01","userId":"u1","type":"Work","start":"06:00","end":"14:00"}
            ```
            Oder Option B:
            ```json
            {"action":"delete","date":"2026-06-01","entryId":"e9"}
            ```
            """;
        var ss = PlannerSuggestionParser.Extract(text);
        Assert.Equal(2, ss.Count);
        Assert.Equal(SuggestionAction.Add, ss[0].Action);
        Assert.Equal(SuggestionAction.Delete, ss[1].Action);
    }

    [Fact]
    public void Extract_PlainTextWithoutCodeBlock_ReturnsEmpty()
        => Assert.Empty(PlannerSuggestionParser.Extract("Lars sollte am 01.06. von 6-14 Uhr arbeiten."));

    [Fact]
    public void Extract_InvalidJson_IsSilentlyIgnored()
        => Assert.Empty(PlannerSuggestionParser.Extract("```json\nthis is not json\n```"));

    [Fact]
    public void Extract_AddMissingMandatoryFields_AreRejected()
    {
        // Add ohne userId
        var noUser = "```json\n{\"action\":\"add\",\"date\":\"2026-06-01\",\"type\":\"Work\",\"start\":\"06:00\",\"end\":\"14:00\"}\n```";
        Assert.Empty(PlannerSuggestionParser.Extract(noUser));
        // Add ohne type
        var noType = "```json\n{\"action\":\"add\",\"date\":\"2026-06-01\",\"userId\":\"u1\",\"start\":\"06:00\",\"end\":\"14:00\"}\n```";
        Assert.Empty(PlannerSuggestionParser.Extract(noType));
    }

    [Fact]
    public void Extract_UnknownAction_IsRejected()
        => Assert.Empty(PlannerSuggestionParser.Extract("```json\n{\"action\":\"clone\",\"date\":\"2026-06-01\",\"entryId\":\"e1\"}\n```"));

    [Fact]
    public void Extract_BadDateFormat_IsRejected()
        => Assert.Empty(PlannerSuggestionParser.Extract("```json\n{\"action\":\"add\",\"date\":\"01.06.2026\",\"userId\":\"u1\",\"type\":\"Work\",\"start\":\"06:00\",\"end\":\"14:00\"}\n```"));
}
