using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Sichtbarkeitsregel für DayNotes im Mail-/PDF-Export. Ergänzt DayNoteAudienceTests, die
/// die UI-Seite prüfen — hier geht's um die Export-Seite, die pro Empfänger neu gerendert wird.
/// </summary>
public class PlanExportNoteForTests
{
    [Fact]
    public void Empty_RawNote_StaysEmpty()
        => Assert.Equal("", PlanExportBuilder.NoteFor("", null, viewerIsAdmin: true, "u1"));

    [Fact]
    public void NoteWithoutAudience_IsVisibleToAnyViewer()
    {
        Assert.Equal("Heute Müll raus", PlanExportBuilder.NoteFor("Heute Müll raus", null, false, "u1"));
        Assert.Equal("Heute Müll raus", PlanExportBuilder.NoteFor("Heute Müll raus", null, false, "u2"));
        Assert.Equal("Heute Müll raus", PlanExportBuilder.NoteFor("Heute Müll raus", null, true, "any"));
    }

    [Fact]
    public void NoteWithAudience_OnlyAddresseeAndAdminSee()
    {
        var note = "Bitte Schlüssel mitnehmen";
        Assert.Equal(note, PlanExportBuilder.NoteFor(note, "lars", false, "lars"));   // Adressat
        Assert.Equal(note, PlanExportBuilder.NoteFor(note, "lars", true, "admin"));   // Admin
        Assert.Equal("", PlanExportBuilder.NoteFor(note, "lars", false, "sneha"));    // andere Person
    }

    [Fact]
    public void NoteWithAudience_Whitespace_IsTreatedAsEmpty()
        => Assert.Equal("", PlanExportBuilder.NoteFor("   ", "lars", true, "admin"));
}
