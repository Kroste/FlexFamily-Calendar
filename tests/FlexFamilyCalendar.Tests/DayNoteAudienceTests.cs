using FlexFamilyCalendar.Models;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Sichtbarkeitsregel des Tages-Hinweises (DayNote.NoteUserId). Die Regel selbst lebt im
/// CalendarViewModel — wir prüfen sie hier als reine Funktion über das Modell, weil das VM
/// ohne UI-Stack schwer testbar ist.
/// </summary>
public class DayNoteAudienceTests
{
    // Spiegelt CalendarViewModel.CanSeeNote: null = alle, sonst Admin oder Adressat.
    private static bool CanSee(string? noteUserId, bool viewerIsAdmin, string viewerId)
    {
        if (string.IsNullOrEmpty(noteUserId)) return true;
        if (viewerIsAdmin) return true;
        return noteUserId == viewerId;
    }

    [Fact]
    public void NoteWithoutAudience_IsVisibleToAll()
    {
        Assert.True(CanSee(null, viewerIsAdmin: false, viewerId: "u1"));
        Assert.True(CanSee(null, viewerIsAdmin: true, viewerId: "u1"));
    }

    [Fact]
    public void NoteWithAudience_VisibleOnlyToAdminAndAddressee()
    {
        Assert.True(CanSee("u1", viewerIsAdmin: false, viewerId: "u1"));   // Adressat
        Assert.True(CanSee("u1", viewerIsAdmin: true, viewerId: "u2"));    // Admin
        Assert.False(CanSee("u1", viewerIsAdmin: false, viewerId: "u2"));  // andere Person
    }

    [Fact]
    public void Parent_CanFinalize_ButIsNotAdmin()
    {
        // Spiegelt CalendarViewModel.CanFinalize = EffectiveIsAdmin || Category==Parent.
        var parent = new User { Role = UserRole.User, Category = PersonCategory.Parent };
        var employee = new User { Role = UserRole.User, Category = PersonCategory.Employee };

        bool CanFin(User u, bool effectiveAdmin) => effectiveAdmin || u.Category == PersonCategory.Parent;

        Assert.True(CanFin(parent, effectiveAdmin: false));
        Assert.False(CanFin(employee, effectiveAdmin: false));
        Assert.True(CanFin(employee, effectiveAdmin: true));   // Admin (Role) trumps Category
    }
}
