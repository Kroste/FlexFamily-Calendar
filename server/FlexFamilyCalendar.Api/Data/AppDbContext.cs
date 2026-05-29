using FlexFamilyCalendar.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexFamilyCalendar.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CalendarEntry> Entries => Set<CalendarEntry>();
    public DbSet<ActivityTypeEntity> ActivityTypes => Set<ActivityTypeEntity>();
    public DbSet<RecurringActivityEntity> RecurringActivities => Set<RecurringActivityEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<CalendarEntry>().HasIndex(e => new { e.UserId, e.Date });
        modelBuilder.Entity<CalendarEntry>().HasIndex(e => e.Status);
    }
}
