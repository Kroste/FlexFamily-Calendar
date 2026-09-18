using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Erzeugt und verwaltet an Benutzer gerichtete Benachrichtigungen (persistiert über <see cref="IStorageService"/>).
/// Texte werden sprach-neutral als Schlüssel + Argumente gespeichert (Lokalisierung erst bei der Anzeige).
/// </summary>
public class NotificationService
{
    private readonly IStorageService _storage;

    public NotificationService(IStorageService storage) => _storage = storage;

    public Task AddAsync(string userId, string messageKey, string? relatedDate, params string[] args)
        => AddManyAsync(new[] { userId }, messageKey, relatedDate, args);

    public async Task AddManyAsync(IEnumerable<string> userIds, string messageKey, string? relatedDate, params string[] args)
    {
        var items = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct()
            .Select(userId => new Notification
            {
                UserId = userId,
                MessageKey = messageKey,
                Args = args.ToList(),
                RelatedDate = relatedDate
            })
            .ToList();
        if (items.Count > 0) await _storage.AddNotificationsAsync(items);
    }

    /// <summary>Krankmeldung an alle Admins — mit Umplanungs-Aktion (Klick öffnet den Umplanungs-Dialog).</summary>
    public async Task AddSickReplanAsync(IEnumerable<string> adminIds, string sickUserId, string relatedDate, string who, string dateLabel)
    {
        var items = adminIds.Where(id => !string.IsNullOrEmpty(id)).Distinct()
            .Select(adminId => new Notification
            {
                UserId = adminId,
                MessageKey = "Notif_SickReported",
                Args = new List<string> { who, dateLabel },
                RelatedDate = relatedDate,
                Action = "ReplanSick",
                RelatedUserId = sickUserId
            })
            .ToList();
        if (items.Count > 0) await _storage.AddNotificationsAsync(items);
    }

    /// <summary>Benachrichtigungen eines Benutzers, neueste zuerst.</summary>
    public async Task<List<Notification>> GetForUserAsync(string userId)
        => (await _storage.LoadNotificationsAsync(userId))
           .Where(n => n.UserId == userId)            // der Server filtert schon; lokal zur Sicherheit
           .OrderByDescending(n => n.CreatedAt)
           .ToList();

    public async Task<int> UnreadCountAsync(string userId)
        => (await _storage.LoadNotificationsAsync(userId)).Count(n => n.UserId == userId && !n.IsRead);

    public Task MarkReadAsync(string userId, string notificationId)
        => _storage.MarkNotificationsReadAsync(userId, new[] { notificationId });

    public Task MarkAllReadAsync(string userId)
        => _storage.MarkNotificationsReadAsync(userId, null);
}
