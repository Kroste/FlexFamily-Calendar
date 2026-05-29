using FlexFamilyCalendar.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexFamilyCalendar.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>().HasIndex(u => u.Username).IsUnique();
    }
}
