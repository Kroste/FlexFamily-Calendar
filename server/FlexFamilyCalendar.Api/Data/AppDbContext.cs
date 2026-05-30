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
    public DbSet<RecurrenceSkipEntity> RecurrenceSkips => Set<RecurrenceSkipEntity>();
    public DbSet<PlannerNoteEntity> PlannerNotes => Set<PlannerNoteEntity>();
    public DbSet<ChatHistoryEntity> ChatHistory => Set<ChatHistoryEntity>();
    public DbSet<ShiftSwapRequestEntity> SwapRequests => Set<ShiftSwapRequestEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<CalendarDayMeta> DayMeta => Set<CalendarDayMeta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<CalendarEntry>().HasIndex(e => new { e.UserId, e.Date });
        modelBuilder.Entity<CalendarEntry>().HasIndex(e => e.Status);
        modelBuilder.Entity<CalendarDayMeta>().HasKey(m => m.Date);

        modelBuilder.Entity<RecurrenceSkipEntity>()
            .HasOne(s => s.RecurringActivity)
            .WithMany(r => r.Skips)
            .HasForeignKey(s => s.RecurringActivityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
