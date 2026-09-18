namespace FlexFamilyCalendar.Api.DayNotes;

/// <summary>
/// Entscheidet serverseitig, wer den Text einer Tagesnotiz sieht. Eine Notiz ohne Adressat gilt
/// für alle; eine adressierte nur für den Admin und die angesprochene Person.
///
/// Vorher lieferten die GET-Endpunkte jede Notiz an jeden Angemeldeten aus, und erst der Client
/// (<c>CanSeeNote</c>) blendete sie aus — in der Web-Ansicht lag eine persönliche Notiz damit
/// für jeden im Netzwerk-Tab der Entwicklerwerkzeuge. Der Client bleibt als zweite Linie
/// bestehen (Impersonate-Sicht), die Grenze zieht der Server.
/// </summary>
public static class DayNoteVisibility
{
    public static (string Note, string? NoteUserId) Project(string? note, string? noteUserId, Guid requesterId, bool isAdmin)
    {
        if (string.IsNullOrEmpty(noteUserId) || isAdmin) return (note ?? "", noteUserId);

        return string.Equals(noteUserId, requesterId.ToString(), StringComparison.OrdinalIgnoreCase)
            ? (note ?? "", noteUserId)
            // Auch der Adressat fällt weg: schon „es gibt eine Notiz an Mara" ist eine Auskunft,
            // die Dritte nichts angeht.
            : ("", null);
    }
}
