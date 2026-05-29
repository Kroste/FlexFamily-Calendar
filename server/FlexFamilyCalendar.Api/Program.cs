using System.Text;
using FlexFamilyCalendar.Api.Auth;
using FlexFamilyCalendar.Api.Data;
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
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db, TokenService tokens) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

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

app.Run();

internal record LoginRequest(string Username, string Password);
