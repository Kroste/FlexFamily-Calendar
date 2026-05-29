using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlexFamilyCalendar.Api.Data;

/// <summary>
/// Nur für `dotnet ef` (Design-Time). Vorhanden, damit die Tools den DbContext direkt
/// erzeugen und NICHT die Startup-/Seed-Logik aus Program.cs ausführen.
/// Zur Laufzeit wird der DbContext per DI gebaut – diese Klasse wird dann nicht genutzt.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                   ?? "Host=localhost;Port=5432;Database=flexfamily;Username=flexfamily;Password=dev";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;
        return new AppDbContext(options);
    }
}
