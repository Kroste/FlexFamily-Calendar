using System.Security.Claims;
using System.Text;
using FlexFamilyCalendar.Api.Auth;
using FlexFamilyCalendar.Api.Data;
using FlexFamilyCalendar.Api.Entries;
using FlexFamilyCalendar.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cfg.GetConnectionString("Default")));
builder.Services.AddScoped<TokenService>();

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

// Schema sicherstellen (Dev). TODO: vor Produktion auf EF-Migrationen umstellen. DB-Start abwarten (Retry).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try { db.Database.EnsureCreated(); break; }
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
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Json(new { error = "Ungültiger Benutzername oder Passwort." },
            statusCode: StatusCodes.Status401Unauthorized);

    return Results.Ok(new
    {
        token = tokens.Create(user),
        user = new { user.Id, user.Username, user.DisplayName, user.Role, user.Category }
    });
});

app.MapGet("/api/users", async (AppDbContext db) =>
    await db.Users.OrderBy(u => u.DisplayName)
        .Select(u => new { u.Id, u.Username, u.DisplayName, u.Email, u.Role, u.Category })
        .ToListAsync())
    .RequireAuthorization("Admin");

app.MapPost("/api/users", async (CreateUserRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Username und Passwort sind erforderlich." });
    if (await db.Users.AnyAsync(u => u.Username == req.Username))
        return Results.Conflict(new { error = "Benutzername bereits vergeben." });

    var user = new UserEntity
    {
        Username = req.Username.Trim(),
        DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? req.Username.Trim() : req.DisplayName!.Trim(),
        Email = req.Email?.Trim() ?? "",
        Role = req.Role == "Admin" ? "Admin" : "User",
        Category = string.IsNullOrWhiteSpace(req.Category) ? "Employee" : req.Category!.Trim(),
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
    };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}",
        new { user.Id, user.Username, user.DisplayName, user.Email, user.Role, user.Category });
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

    var valError = EntryWriteRules.Validate(req.Type, req.Date, req.EndDate, req.StartTime, req.EndTime, req.CategoryLabel);
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

    var valError = EntryWriteRules.Validate(entry.Type, req.Date, req.EndDate, req.StartTime, req.EndTime, req.CategoryLabel);
    if (valError is not null) return Results.BadRequest(new { error = valError });

    entry.Date = req.Date;
    entry.EndDate = req.EndDate;
    entry.StartTime = req.StartTime;
    entry.EndTime = req.EndTime;
    entry.EndsNextDay = req.EndsNextDay;
    entry.CategoryLabel = string.IsNullOrWhiteSpace(req.CategoryLabel) ? null : req.CategoryLabel!.Trim();
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

app.Run();

internal record LoginRequest(string Username, string Password);
internal record CreateUserRequest(string Username, string Password, string? DisplayName, string? Email, string? Role, string? Category);
