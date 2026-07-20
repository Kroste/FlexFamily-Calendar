namespace FlexFamilyCalendar.Models;

// Werte explizit gepinnt (Lücke bei 1 = ehemaliges AuPairShift), damit bestehende
// als Integer gespeicherte Einträge gültig bleiben. Alt-Wert 1 wird beim Laden → Work gemappt.
public enum EntryType
{
    Work = 0,
    Vacation = 2,
    SickLeave = 3,
    Activity = 4,
    Absence = 5,
    Overnight = 6,  // Übernachtung (auf Abruf): volle Zeit sichtbar, zählt pauschal x Std./Tag aufs Konto
    Custom = 7      // Freier Zeitblock mit Titel (kein Work — zählt NICHT zur Arbeitszeit)
}
