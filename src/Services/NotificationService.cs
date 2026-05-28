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

    public async Task AddAsync(string userId, string messageKey, string? relatedDate, params string[] args)
    {
        if (string.IsNullOrEmpty(userId)) return;
        var all = await _storage.LoadNotificationsAsync();
        all.Add(new Notification
        {
            UserId = userId,
            MessageKey = messageKey,
            Args = args.ToList(),
            RelatedDate = relatedDate
        });
        await _storage.SaveNotificationsAsync(all);
    }

    public async Task AddManyAsync(IEnumerable<string> userIds, string messageKey, string? relatedDate, params string[] args)
    {
        var targets = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (targets.Count == 0) return;
        var all = await _storage.LoadNotificationsAsync();
        foreach (var userId in targets)
            all.Add(new Notification
            {
                UserId = userId,
                MessageKey = messageKey,
                Args = args.ToList(),
                RelatedDate = relatedDate
            });
        await _storage.SaveNotificationsAsync(all);
    }

    /// <summary>Benachrichtigungen eines Benutzers, neueste zuerst.</summary>
    public async Task<List<Notification>> GetForUserAsync(string userId)
    {
        var all = await _storage.LoadNotificationsAsync();
        return all.Where(n => n.UserId == userId)
                  .OrderByDescending(n => n.CreatedAt)
                  .ToList();
    }

    public async Task<int> UnreadCountAsync(string userId)
    {
        var all = await _storage.LoadNotificationsAsync();
        return all.Count(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkReadAsync(string notificationId)
    {
        var all = await _storage.LoadNotificationsAsync();
        var n = all.FirstOrDefault(x => x.Id == notificationId);
        if (n is { IsRead: false })
        {
            n.IsRead = true;
            await _storage.SaveNotificationsAsync(all);
        }
    }

    public async Task MarkAllReadAsync(string userId)
    {
        var all = await _storage.LoadNotificationsAsync();
        var changed = false;
        foreach (var n in all.Where(n => n.UserId == userId && !n.IsRead))
        {
            n.IsRead = true;
            changed = true;
        }
        if (changed) await _storage.SaveNotificationsAsync(all);
    }
}
