using System.Security.Claims;
using System.Text;
using FlexFamilyCalendar.Api.Auth;
using FlexFamilyCalendar.Api.Data;
using FlexFamilyCalendar.Api.ActivityTypes;
using FlexFamilyCalendar.Api.DayNotes;
using FlexFamilyCalendar.Api.Ai;
using FlexFamilyCalendar.Api.Entries;
using FlexFamilyCalendar.Api.Mail;
using FlexFamilyCalendar.Api.Models;
using FlexFamilyCalendar.Api.Notifications;
using FlexFamilyCalendar.Api.RecurringActivities;
using FlexFamilyCalendar.Api.Swaps;
using FlexFamilyCalendar.Api.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cfg.GetConnectionString("Default")));
builder.Services.AddScoped<TokenService>();

// SMTP über ENV (Smtp__Host etc.) — Operator-Setting, kein DB-Schema.
builder.Services.AddSingleton(new SmtpOptions
{
    Host = cfg["Smtp:Host"] ?? "",
    Port = int.TryParse(cfg["Smtp:Port"], out var smtpPort) ? smtpPort : 587,
    From = cfg["Smtp:From"] ?? "",
    User = cfg["Smtp:User"] ?? "",
    Password = cfg["Smtp:Password"] ?? "",
    UseSsl = !string.Equals(cfg["Smtp:UseSsl"], "false", StringComparison.OrdinalIgnoreCase)
});
builder.Services.AddScoped<MailSender>();

// AI über ENV (Ai__<Provider>__Key) — Operator-Setting. Client schickt Provider+Prompt,
// Server proxiet zum Cloud-Provider mit dem ENV-Schlüssel; CORS und Geheimnis bleiben
// dadurch serverseitig.
builder.Services.AddSingleton(new AiOptions
{
    AnthropicKey = cfg["Ai:Anthropic:Key"] ?? "",
    OpenAiKey = cfg["Ai:OpenAi:Key"] ?? "",
    GeminiKey = cfg["Ai:Gemini:Key"] ?? "",
    PerplexityKey = cfg["Ai:Perplexity:Key"] ?? ""
});
builder.Services.AddHttpClient<AiSender>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = cfg["Jwt:Issuer"],
        ValidAudience = cfg["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!))
    });
builder.Services.AddAuthorization(o => o.AddPolicy("Admin", p => p.RequireRole("Admin")));

var app = builder.Build();

// Ausstehende EF-Migrationen anwenden (legt das Schema an bzw. aktualisiert es). DB-Start abwarten (Retry).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try { db.Database.Migrate(); break; }
        catch when (attempt < 10) { Thread.Sleep(3000); }   // DB evtl. noch nicht bereit
    }

    // Erst-Admin anlegen (idempotent): nur wenn per Konfiguration gesetzt UND noch keine Benutzer existieren.
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var seedUser = cfg["Seed:AdminUsername"];
    var seedPass = cfg["Seed:AdminPassword"];
    if (string.IsNullOrWhiteSpace(seedUser) || string.IsNullOrWhiteSpace(seedPass))
        logger.LogWarning("Kein Erst-Admin angelegt: Seed__AdminUsername/Seed__AdminPassword sind nicht gesetzt.");
    else if (db.Users.Any())
        logger.LogInformation("Seed übersprungen: es existieren bereits Benutzer.");
    else
    {
        db.Users.Add(new UserEntity
        {
            Username = seedUser.Trim(),
            DisplayName = seedUser.Trim(),
            Role = "Admin",
            Category = "Parent",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedPass)
        });
        db.SaveChanges();
        logger.LogInformation("Erst-Admin '{User}' angelegt.", seedUser.Trim());
    }
}

app.UseAuthentication();
app.UseAuthorization();

// Benutzer-Id des Anfragenden aus dem JWT (robust gegen verschiedene Claim-Namen).
static Guid? CurrentUserId(ClaimsPrincipal p)
{
    var raw = p.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? p.FindFirstValue("sub")
              ?? p.FindFirstValue("nameid");
    return Guid.TryParse(raw, out var id) ? id : null;
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db, TokenService tokens) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
    // Leerer Hash = Konto ohne Anmeldung (z.B. Kind) → nie anmeldbar (und BCrypt.Verify würfe sonst).
    if (user is null || string.IsNullOrEmpty(user.PasswordHash)
        || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Json(new { error = "Ungültiger Benutzername oder Passwort." },
            statusCode: StatusCodes.Status401Unauthorized);

    return Results.Ok(new
    {
        token = tokens.Create(user),
        user = UserDto.From(user)
    });
});

// Aktueller Benutzer laut Token — für „Login merken": Client prüft beim Start ein gespeichertes Token.
app.MapGet("/api/auth/me", async (AppDbContext db, ClaimsPrincipal principal) =>
{
    var id = CurrentUserId(principal);
    if (id is null) return Results.Unauthorized();
    var user = await db.Users.FindAsync(id.Value);
    return user is null ? Results.Unauthorized() : Results.Ok(UserDto.From(user));
})
    .RequireAuthorization();

// Eigenes Profil ändern (jeder für sich) — NUR selbst-editierbare Felder, niemals Rolle/Kategorie/Stunden.
app.MapPut("/api/auth/me", async (UpdateProfileRequest req, AppDbContext db, ClaimsPrincipal principal) =>
{
    var id = CurrentUserId(principal);
    if (id is null) return Results.Unauthorized();
    var user = await db.Users.FindAsync(id.Value);
    if (user is null) return Results.Unauthorized();

    if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.DisplayName = req.DisplayName.Trim();
    if (req.Email is not null) user.Email = req.Email.Trim();
    if (!string.IsNullOrWhiteSpace(req.Language)) user.Language = req.Language.Trim();
    if (req.Color is not null) user.Color = req.Color.Trim();

    await db.SaveChangesAsync();
    return Results.Ok(UserDto.From(user));
})
    .RequireAuthorization();

// Eigenes Kennwort ändern (jeder für sich).
app.MapPost("/api/auth/me/password", async (SetPasswordRequest req, AppDbContext db, ClaimsPrincipal principal) =>
{
    if (string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Kennwort darf nicht leer sein." });
    var id = CurrentUserId(principal);
    if (id is null) return Results.Unauthorized();
    var user = await db.Users.FindAsync(id.Value);
    if (user is null) return Results.Unauthorized();

    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
    .RequireAuthorization();

app.MapGet("/api/users", async (AppDbContext db) =>
    (await db.Users.OrderBy(u => u.DisplayName).ToListAsync()).Select(UserDto.From))
    .RequireAuthorization("Admin");

app.MapPost("/api/users", async (CreateUserRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Username))
        return Results.BadRequest(new { error = "Benutzername ist erforderlich." });
    if (await db.Users.AnyAsync(u => u.Username == req.Username))
        return Results.Conflict(new { error = "Benutzername bereits vergeben." });

    var user = new UserEntity
    {
        Username = req.Username.Trim(),
        DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? req.Username.Trim() : req.DisplayName!.Trim(),
        Email = req.Email?.Trim() ?? "",
        Role = UserRules.NormalizeRole(req.Role),
        Category = string.IsNullOrWhiteSpace(req.Category) ? "Employee" : req.Category!.Trim(),
        WeeklyHoursQuota = req.WeeklyHoursQuota,
        MaxWeeklyHours = req.MaxWeeklyHours,
        MaxDailyHours = req.MaxDailyHours,
        MinRestHours = req.MinRestHours,
        Color = req.Color?.Trim() ?? "",
        Language = string.IsNullOrWhiteSpace(req.Language) ? "de" : req.Language!.Trim(),
        // Leeres Passwort erlaubt = Konto ohne Anmeldung (z.B. Kind).
        PasswordHash = string.IsNullOrWhiteSpace(req.Password) ? "" : BCrypt.Net.BCrypt.HashPassword(req.Password)
    };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", UserDto.From(user));
})
    .RequireAuthorization("Admin");

app.MapPut("/api/users/{id:guid}", async (Guid id, UpdateUserRequest req, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();

    if (string.IsNullOrWhiteSpace(req.Username))
        return Results.BadRequest(new { error = "Benutzername darf nicht leer sein." });
    if (await db.Users.AnyAsync(u => u.Id != id && u.Username == req.Username))
        return Results.Conflict(new { error = "Benutzername bereits vergeben." });

    var newRole = UserRules.NormalizeRole(req.Role);
    var totalAdmins = await db.Users.CountAsync(u => u.Role == UserRules.Admin);
    var roleError = UserRules.CheckRoleChange(user.Role == UserRules.Admin, newRole == UserRules.Admin, totalAdmins);
    if (roleError is not null) return Results.BadRequest(new { error = roleError });

    user.Username = req.Username.Trim();
    user.DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? user.Username : req.DisplayName!.Trim();
    user.Email = req.Email?.Trim() ?? "";
    user.Role = newRole;
    user.Category = string.IsNullOrWhiteSpace(req.Category) ? user.Category : req.Category!.Trim();
    user.WeeklyHoursQuota = req.WeeklyHoursQuota;
    user.MaxWeeklyHours = req.MaxWeeklyHours;
    user.MaxDailyHours = req.MaxDailyHours;
    user.MinRestHours = req.MinRestHours;
    if (req.Color is not null) user.Color = req.Color.Trim();
    if (!string.IsNullOrWhiteSpace(req.Language)) user.Language = req.Language!.Trim();

    await db.SaveChangesAsync();
    return Results.Ok(UserDto.From(user));
})
    .RequireAuthorization("Admin");

app.MapDelete("/api/users/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();

    var totalAdmins = await db.Users.CountAsync(u => u.Role == UserRules.Admin);
    var delError = UserRules.CheckDelete(user.Role == UserRules.Admin, totalAdmins);
    if (delError is not null) return Results.BadRequest(new { error = delError });

    // Einträge des Benutzers mitlöschen, damit keine verwaisten Schichten zurückbleiben.
    var entries = await db.Entries.Where(e => e.UserId == id).ToListAsync();
    db.Entries.RemoveRange(entries);
    db.Users.Remove(user);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
    .RequireAuthorization("Admin");

app.MapPost("/api/users/{id:guid}/password", async (Guid id, SetPasswordRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Kennwort darf nicht leer sein." });
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();

    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
    .RequireAuthorization("Admin");

// --- Kalender-Einträge ---------------------------------------------------

// Woche/Zeitraum laden. Serverseitig maskiert: Fremde sehen private Einträge (Urlaub/Krank)
// nur als „Abwesend" und ungenehmigte Einträge gar nicht.
app.MapGet("/api/entries", async (DateOnly from, DateOnly to, AppDbContext db, ClaimsPrincipal principal) =>
{
    var requester = CurrentUserId(principal);
    if (requester is null) return Results.Unauthorized();
    var isAdmin = principal.IsInRole("Admin");

    var raw = await db.Entries
        .Where(e => e.Date <= to && (e.EndDate ?? e.Date) >= from)
        .ToListAsync();

    var dtos = raw.Select(e => EntryVisibility.Project(e, requester.Value, isAdmin))
                  .Where(d => d is not null)
                  .ToList();
    return Results.Ok(dtos);
})
    .RequireAuthorization();

// Offene Urlaubswünsche (Genehmigungs-Warteschlange) – nur Admin.
app.MapGet("/api/entries/pending", async (AppDbContext db) =>
{
    var list = await db.Entries.Where(e => e.Status == EntryStatus.Pending)
        .OrderBy(e => e.Date).ToListAsync();
    return Results.Ok(list.Select(EntryDto.Full));
})
    .RequireAuthorization("Admin");

// Eintrag anlegen. Admin: alles für jeden. Nicht-Admin: nur eigener Urlaubswunsch/Krankmeldung.
app.MapPost("/api/entries", async (CreateEntryRequest req, AppDbContext db, ClaimsPrincipal principal) =>
{
    var requester = CurrentUserId(principal);
    if (requester is null) return Results.Unauthorized();
    var isAdmin = principal.IsInRole("Admin");
    var targetUserId = req.UserId ?? requester.Value;

    var permError = EntryWriteRules.CheckCreate(req.Type, targetUserId, requester.Value, isAdmin);
    if (permError is not null)
        return Results.Json(new { error = permError }, statusCode: StatusCodes.Status403Forbidden);

    var valError = EntryWriteRules.Validate(req.Type, req.Date, req.EndDate, req.StartTime, req.EndTime, req.CategoryLabel, req.ActivityTypeId);
    if (valError is not null) return Results.BadRequest(new { error = valError });

    if (!await db.Users.AnyAsync(u => u.Id == targetUserId))
        return Results.BadRequest(new { error = "Benutzer existiert nicht." });

    var entry = new CalendarEntry
    {
        UserId = targetUserId,
        Type = req.Type,
        Date = req.Date,
        EndDate = req.EndDate,
        StartTime = req.StartTime,
        EndTime = req.EndTime,
        EndsNextDay = req.EndsNextDay,
        CategoryLabel = string.IsNullOrWhiteSpace(req.CategoryLabel) ? null : req.CategoryLabel!.Trim(),
        ActivityTypeId = string.IsNullOrWhiteSpace(req.ActivityTypeId) ? null : req.ActivityTypeId!.Trim(),
        Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note!.Trim(),
        Status = EntryWriteRules.InitialStatus(req.Type, isAdmin),
        CreatedBy = requester.Value,
        CreatedAtUtc = DateTime.UtcNow
    };
    db.Entries.Add(entry);
    await db.SaveChangesAsync();
    return Results.Created($"/api/entries/{entry.Id}", EntryDto.Full(entry));
})
    .RequireAuthorization();

// Eintrag bearbeiten. Admin: jeden. Nicht-Admin: nur eigene; ein geänderter Urlaub muss neu genehmigt werden.
app.MapPut("/api/entries/{id:guid}", async (Guid id, UpdateEntryRequest req, AppDbContext db, ClaimsPrincipal principal) =>
{
    var requester = CurrentUserId(principal);
    if (requester is null) return Results.Unauthorized();
    var isAdmin = principal.IsInRole("Admin");

    var entry = await db.Entries.FindAsync(id);
    if (entry is null) return Results.NotFound();
    if (!isAdmin && entry.UserId != requester.Value)
        return Results.Json(new { error = "Kein Zugriff." }, statusCode: StatusCodes.Status403Forbidden);

    var valError = EntryWriteRules.Validate(entry.Type, req.Date, req.EndDate, req.StartTime, req.EndTime, req.CategoryLabel, req.ActivityTypeId);
    if (valError is not null) return Results.BadRequest(new { error = valError });

    entry.Date = req.Date;
    entry.EndDate = req.EndDate;
    entry.StartTime = req.StartTime;
    entry.EndTime = req.EndTime;
    entry.EndsNextDay = req.EndsNextDay;
    entry.CategoryLabel = string.IsNullOrWhiteSpace(req.CategoryLabel) ? null : req.CategoryLabel!.Trim();
    entry.ActivityTypeId = string.IsNullOrWhiteSpace(req.ActivityTypeId) ? null : req.ActivityTypeId!.Trim();
    entry.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note!.Trim();

    if (!isAdmin && entry.Type == EntryTypes.Vacation)
        entry.Status = EntryStatus.Pending;

    await db.SaveChangesAsync();
    return Results.Ok(EntryDto.Full(entry));
})
    .RequireAuthorization();

// Eintrag löschen. Admin: jeden. Nicht-Admin: nur eigene.
app.MapDelete("/api/entries/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal principal) =>
{
    var requester = CurrentUserId(principal);
    if (requester is null) return Results.Unauthorized();
    var isAdmin = principal.IsInRole("Admin");

    var entry = await db.Entries.FindAsync(id);
    if (entry is null) return Results.NotFound();
    if (!isAdmin && entry.UserId != requester.Value)
        return Results.Json(new { error = "Kein Zugriff." }, statusCode: StatusCodes.Status403Forbidden);

    db.Entries.Remove(entry);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
    .RequireAuthorization();

// Urlaubswunsch genehmigen / ablehnen – nur Admin.
app.MapPost("/api/entries/{id:guid}/approve", async (Guid id, AppDbContext db) =>
{
    var entry = await db.Entries.FindAsync(id);
    if (entry is null) return Results.NotFound();
    entry.Status = EntryStatus.Approved;
    await db.SaveChangesAsync();
    return Results.Ok(EntryDto.Full(entry));
})
    .RequireAuthorization("Admin");

app.MapPost("/api/entries/{id:guid}/reject", async (Guid id, AppDbContext db) =>
{
    var entry = await db.Entries.FindAsync(id);
    if (entry is null) return Results.NotFound();
    entry.Status = EntryStatus.Rejected;
    await db.SaveChangesAsync();
    return Results.Ok(EntryDto.Full(entry));
})
    .RequireAuthorization("Admin");

// --- Aktivitätstypen (Kategorien) ---------------------------------------

// Liste: alle angemeldeten Benutzer (zum Anzeigen von Kategoriename/-farbe im Plan).
app.MapGet("/api/activity-types", async (AppDbContext db) =>
    (await db.ActivityTypes.OrderBy(a => a.Name).ToListAsync()).Select(ActivityTypeDto.From))
    .RequireAuthorization();

// Komplett ersetzen (Admin): passt zum „ganze Liste speichern" des Clients. Letzter-Schreiber-gewinnt.
app.MapPut("/api/activity-types", async (List<ActivityTypeDto> items, AppDbContext db) =>
{
    await db.ActivityTypes.ExecuteDeleteAsync();
    foreach (var i in items)
        db.ActivityTypes.Add(new ActivityTypeEntity
        {
            Id = i.Id == Guid.Empty ? Guid.NewGuid() : i.Id,
            Name = i.Name.Trim(),
            Color = i.Color ?? "",
            Categories = i.Categories ?? new()
        });
    await db.SaveChangesAsync();
    return Results.Ok((await db.ActivityTypes.OrderBy(a => a.Name).ToListAsync()).Select(ActivityTypeDto.From));
})
    .RequireAuthorization("Admin");

// --- Wiederkehrende Aktivitäten ------------------------------------------

// Liste: alle Angemeldeten (das Overlay wird in jedem Plan projiziert).
app.MapGet("/api/recurring-activities", async (AppDbContext db) =>
    (await db.RecurringActivities.ToListAsync()).Select(RecurringActivityDto.From))
    .RequireAuthorization();

// Komplett ersetzen (Admin) — passt zum „ganze Liste speichern" des Clients.
app.MapPut("/api/recurring-activities", async (List<RecurringActivityDto> items, AppDbContext db) =>
{
    await db.RecurringActivities.ExecuteDeleteAsync();
    foreach (var i in items)
        db.RecurringActivities.Add(new RecurringActivityEntity
        {
            Id = i.Id == Guid.Empty ? Guid.NewGuid() : i.Id,
            UserId = i.UserId,
            UserDisplayName = i.UserDisplayName ?? "",
            Title = i.Title ?? "",
            ActivityTypeId = string.IsNullOrWhiteSpace(i.ActivityTypeId) ? null : i.ActivityTypeId,
            StartTime = i.StartTime,
            EndTime = i.EndTime,
            Weekdays = i.Weekdays ?? new(),
            SkipOnHolidays = i.SkipOnHolidays
        });
    await db.SaveChangesAsync();
    return Results.Ok((await db.RecurringActivities.ToListAsync()).Select(RecurringActivityDto.From));
})
    .RequireAuthorization("Admin");

// --- Schichttausch -------------------------------------------------------
// Hinweis: Speichern ersetzt die ganze Liste (passt zum Client). Da jeder Mitarbeiter Tausch
// anlegt/beantwortet, ist PUT für alle Angemeldeten offen → letzter-Schreiber-gewinnt; eine
// granulare Tausch-API (anlegen/annehmen/ablehnen mit Rechteprüfung) ist eine spätere Verfeinerung.

app.MapGet("/api/swap-requests", async (AppDbContext db) =>
    (await db.SwapRequests.ToListAsync()).Select(ShiftSwapRequestDto.From))
    .RequireAuthorization();

app.MapPut("/api/swap-requests", async (List<ShiftSwapRequestDto> items, AppDbContext db) =>
{
    await db.SwapRequests.ExecuteDeleteAsync();
    foreach (var i in items)
        db.SwapRequests.Add(new ShiftSwapRequestEntity
        {
            Id = i.Id == Guid.Empty ? Guid.NewGuid() : i.Id,
            CreatedAt = i.CreatedAt ?? "",
            RespondedAt = i.RespondedAt,
            Status = i.Status,
            Mode = i.Mode,
            FromUserId = i.FromUserId ?? "",
            FromUserName = i.FromUserName ?? "",
            FromDate = i.FromDate ?? "",
            FromEntryId = i.FromEntryId ?? "",
            ToUserId = i.ToUserId ?? "",
            ToUserName = i.ToUserName ?? "",
            ToDate = i.ToDate,
            ToEntryId = i.ToEntryId,
            Message = i.Message ?? ""
        });
    await db.SaveChangesAsync();
    return Results.Ok((await db.SwapRequests.ToListAsync()).Select(ShiftSwapRequestDto.From));
})
    .RequireAuthorization();

// --- Benachrichtigungen --------------------------------------------------
// Wie Schichttausch: Replace-all (passt zum Client), für alle Angemeldeten. Gleiche Grenze
// (letzter-Schreiber-gewinnt; Filterung nach Empfänger macht weiterhin der Client).

app.MapGet("/api/notifications", async (AppDbContext db) =>
    (await db.Notifications.ToListAsync()).Select(NotificationDto.From))
    .RequireAuthorization();

app.MapPut("/api/notifications", async (List<NotificationDto> items, AppDbContext db) =>
{
    await db.Notifications.ExecuteDeleteAsync();
    foreach (var i in items)
        db.Notifications.Add(new NotificationEntity
        {
            Id = i.Id == Guid.Empty ? Guid.NewGuid() : i.Id,
            UserId = i.UserId ?? "",
            CreatedAt = i.CreatedAt ?? "",
            IsRead = i.IsRead,
            MessageKey = i.MessageKey ?? "",
            Args = i.Args ?? new(),
            RelatedDate = i.RelatedDate,
            Action = i.Action,
            RelatedUserId = i.RelatedUserId
        });
    await db.SaveChangesAsync();
    return Results.Ok((await db.Notifications.ToListAsync()).Select(NotificationDto.From));
})
    .RequireAuthorization();

// --- Tagesnotiz / Finalisiert (pro Datum) --------------------------------

app.MapGet("/api/day-notes/{date}", async (DateOnly date, AppDbContext db) =>
{
    var meta = await db.DayMeta.FindAsync(date);
    return Results.Ok(new DayNoteDto(meta?.Note ?? "", meta?.IsFinalized ?? false));
})
    .RequireAuthorization();

// Setzen ist Admin-Sache (allgemeiner Tageshinweis + Finalisieren). Leere Notiz ohne Finalisiert → Zeile entfernen.
app.MapPut("/api/day-notes/{date}", async (DateOnly date, DayNoteDto body, AppDbContext db) =>
{
    var meta = await db.DayMeta.FindAsync(date);
    if (string.IsNullOrWhiteSpace(body.Note) && !body.IsFinalized)
    {
        if (meta is not null) { db.DayMeta.Remove(meta); await db.SaveChangesAsync(); }
        return Results.NoContent();
    }
    if (meta is null) { meta = new CalendarDayMeta { Date = date }; db.DayMeta.Add(meta); }
    meta.Note = body.Note?.Trim() ?? "";
    meta.IsFinalized = body.IsFinalized;
    await db.SaveChangesAsync();
    return Results.Ok(new DayNoteDto(meta.Note, meta.IsFinalized));
})
    .RequireAuthorization("Admin");

// Mail: Wochenplan-PDF an mehrere Empfänger senden. Jeder Empfänger bekommt sein vom Client
// vorab gerendertes, aus seiner Sicht maskiertes PDF — der Server kennt die Anzeige-Regeln
// nicht und vertraut dem Admin-Client. Serverseitig nur SMTP-Versand.
app.MapPost("/api/mail/send-week-plan", async (SendWeekPlanRequest req, MailSender sender) =>
{
    if (string.IsNullOrWhiteSpace(req.Subject) || req.Recipients is null || req.Recipients.Count == 0)
        return Results.BadRequest(new { error = "subject und recipients sind Pflicht." });

    var sent = 0;
    var failed = 0;
    var errors = new List<string>();
    foreach (var r in req.Recipients)
    {
        if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.PdfBase64))
        {
            failed++;
            errors.Add($"{r.Email}: Adresse oder PDF leer.");
            continue;
        }
        try
        {
            var pdf = Convert.FromBase64String(r.PdfBase64);
            var (ok, error) = await sender.SendAsync(r.Email, req.Subject, req.Body ?? "", pdf, req.FileName ?? "Plan.pdf");
            if (ok) sent++;
            else { failed++; if (error is not null) errors.Add($"{r.Email}: {error}"); }
        }
        catch (FormatException)
        {
            failed++;
            errors.Add($"{r.Email}: PDF-Base64 ungültig.");
        }
    }
    return Results.Ok(new SendWeekPlanResponse(sent, failed, errors));
})
    .RequireAuthorization("Admin");

// AI-Proxy: Client schickt Provider+Prompt, Server ruft den jeweiligen Cloud-Endpoint mit dem
// serverseitigen API-Key. Eingeloggte User dürfen — KI-Vorschläge sind nicht admin-exklusiv.
app.MapPost("/api/ai/complete", async (AiCompleteRequest req, AiSender sender, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Provider) || string.IsNullOrWhiteSpace(req.Prompt))
        return Results.BadRequest(new { error = "provider und prompt sind Pflicht." });

    var (ok, text, error) = await sender.CompleteAsync(req.Provider, req.Prompt, req.Model, ct);
    if (!ok) return Results.Json(new { error = error ?? "Unbekannter Fehler" }, statusCode: StatusCodes.Status502BadGateway);
    return Results.Ok(new AiCompleteResponse(text));
})
    .RequireAuthorization();

app.Run();

internal record LoginRequest(string Username, string Password);

internal record CreateUserRequest(
    string Username, string Password, string? DisplayName, string? Email, string? Role, string? Category,
    double WeeklyHoursQuota = 0, double MaxWeeklyHours = 0, double MaxDailyHours = 0, double MinRestHours = 0,
    string? Color = null, string? Language = null);

internal record UpdateUserRequest(
    string Username, string? DisplayName, string? Email, string? Role, string? Category,
    double WeeklyHoursQuota = 0, double MaxWeeklyHours = 0, double MaxDailyHours = 0, double MinRestHours = 0,
    string? Color = null, string? Language = null);

internal record SetPasswordRequest(string Password);

internal record UpdateProfileRequest(string? DisplayName, string? Email, string? Language, string? Color);
