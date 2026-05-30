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
    public void NoteWithAudience_OnlyAddresseeSees_AdminGetsNoBypass()
    {
        // Strikt persönlich: Admin-Empfänger wird im Mail-Export nicht bevorzugt, weil sonst
        // der personalisierte Hinweis durch das Broadcast ins Admin-Postfach rutscht.
        var note = "Bitte Schlüssel mitnehmen";
        Assert.Equal(note, PlanExportBuilder.NoteFor(note, "lars", viewerIsAdmin: false, "lars"));  // Adressat
        Assert.Equal(note, PlanExportBuilder.NoteFor(note, "lars", viewerIsAdmin: true, "lars"));   // Adressat ist gleichzeitig Admin → ok
        Assert.Equal("", PlanExportBuilder.NoteFor(note, "lars", viewerIsAdmin: true, "admin"));    // Admin ≠ Adressat → leer
        Assert.Equal("", PlanExportBuilder.NoteFor(note, "lars", viewerIsAdmin: false, "sneha"));   // andere Person
    }

    [Fact]
    public void NoteWithAudience_Whitespace_IsTreatedAsEmpty()
        => Assert.Equal("", PlanExportBuilder.NoteFor("   ", "lars", true, "admin"));
}
