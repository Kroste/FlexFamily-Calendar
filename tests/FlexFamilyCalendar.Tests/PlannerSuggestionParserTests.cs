using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.AI;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class PlannerSuggestionParserTests
{
    [Fact]
    public void Extract_FromJsonBlock_ReturnsSuggestion()
    {
        var text = """
            Ich schlage vor, dass Lars die Frühschicht übernimmt:
            ```json
            {"action":"add","date":"2026-06-01","userId":"u1","type":"Work","start":"06:00","end":"14:00","title":"Frühschicht"}
            ```
            Passt das?
            """;
        var s = Assert.Single(PlannerSuggestionParser.Extract(text));
        Assert.Equal("add", s.Action);
        Assert.Equal(new DateOnly(2026, 6, 1), s.Date);
        Assert.Equal("u1", s.UserId);
        Assert.Equal(EntryType.Work, s.Type);
        Assert.Equal(TimeSpan.FromHours(6), s.Start);
        Assert.Equal(TimeSpan.FromHours(14), s.End);
        Assert.Equal("Frühschicht", s.Title);
    }

    [Fact]
    public void Extract_WithoutLanguageHint_AlsoWorks()
    {
        var text = "```\n{\"action\":\"add\",\"date\":\"2026-06-02\",\"userId\":\"u2\",\"type\":\"Activity\",\"start\":\"16:00\",\"end\":\"17:30\"}\n```";
        var s = Assert.Single(PlannerSuggestionParser.Extract(text));
        Assert.Equal(EntryType.Activity, s.Type);
        Assert.Null(s.Title);
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
            {"action":"add","date":"2026-06-01","userId":"u2","type":"Work","start":"14:00","end":"22:00"}
            ```
            """;
        var ss = PlannerSuggestionParser.Extract(text);
        Assert.Equal(2, ss.Count);
        Assert.Equal("u1", ss[0].UserId);
        Assert.Equal("u2", ss[1].UserId);
    }

    [Fact]
    public void Extract_PlainTextWithoutCodeBlock_ReturnsEmpty()
    {
        var text = "Lars sollte am 01.06. von 6-14 Uhr arbeiten.";
        Assert.Empty(PlannerSuggestionParser.Extract(text));
    }

    [Fact]
    public void Extract_InvalidJson_IsSilentlyIgnored()
    {
        var text = "```json\nthis is not json\n```";
        Assert.Empty(PlannerSuggestionParser.Extract(text));
    }

    [Fact]
    public void Extract_MissingFields_AreRejected()
    {
        var text = "```json\n{\"action\":\"add\",\"date\":\"2026-06-01\"}\n```";   // ohne userId/type/start/end
        Assert.Empty(PlannerSuggestionParser.Extract(text));
    }

    [Fact]
    public void Extract_UnsupportedAction_IsRejected()
    {
        var text = "```json\n{\"action\":\"delete\",\"date\":\"2026-06-01\",\"userId\":\"u1\",\"type\":\"Work\",\"start\":\"06:00\",\"end\":\"14:00\"}\n```";
        Assert.Empty(PlannerSuggestionParser.Extract(text));
    }

    [Fact]
    public void Extract_BadDateFormat_IsRejected()
    {
        var text = "```json\n{\"action\":\"add\",\"date\":\"01.06.2026\",\"userId\":\"u1\",\"type\":\"Work\",\"start\":\"06:00\",\"end\":\"14:00\"}\n```";
        Assert.Empty(PlannerSuggestionParser.Extract(text));
    }
}
