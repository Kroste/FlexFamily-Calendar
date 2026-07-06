using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlexFamilyCalendar.Api.Data;
using FlexFamilyCalendar.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlexFamilyCalendar.Api.Tests;

/// <summary>
/// WebApplicationFactory für Integration-Tests. Program.cs überspringt im "Testing"-Environment
/// die Npgsql-Registrierung; hier registrieren wir den DbContext mit InMemory und seeden
/// nach dem Start einen Admin + User. Eigene DB-Instanz pro Fixture-Instanz → Test-Isolation.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string JwtIssuer = "flexfamily-tests";
    public const string JwtAudience = "flexfamily-tests";
    public const string JwtKey = "test-signing-key-must-be-long-enough-for-hs256-abcdefgh";

    public const string AdminUser = "admin";
    public const string AdminPassword = "adminpass";
    public const string PlainUser = "user";
    public const string PlainPassword = "userpass";

    private readonly string _dbName = "test-" + Guid.NewGuid();
    private readonly object _seedLock = new();
    private bool _seeded;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:Key"] = JwtKey
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    private void SeedIfNeeded()
    {
        if (_seeded) return;
        lock (_seedLock)
        {
            if (_seeded) return;
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            if (!db.Users.Any())
            {
                db.Users.Add(new UserEntity
                {
                    Username = AdminUser,
                    DisplayName = "Admin",
                    Role = "Admin",
                    Category = "Parent",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword)
                });
                db.Users.Add(new UserEntity
                {
                    Username = PlainUser,
                    DisplayName = "User",
                    Role = "User",
                    Category = "Employee",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(PlainPassword)
                });
                db.SaveChanges();
            }
            _seeded = true;
        }
    }

    public HttpClient CreateSeededClient()
    {
        // Client erst erzeugen (startet den Test-Server), DANN seeden — vorher hat Services
        // noch keinen ServiceProvider.
        var client = CreateClient();
        SeedIfNeeded();
        return client;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string username, string password)
    {
        var client = CreateSeededClient();
        var resp = await client.PostAsJsonAsync("api/auth/login", new { username, password });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private record LoginResponseDto(string Token, object User);
}
