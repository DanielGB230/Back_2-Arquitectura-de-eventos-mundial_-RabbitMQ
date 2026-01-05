using Backend.Consumidor.Api.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Consumidor.Api.Data;

public class MatchDbContext : DbContext
{
    public MatchDbContext(DbContextOptions<MatchDbContext> options) : base(options)
    {
    }

    public DbSet<Match> Matches { get; set; }
    public DbSet<MatchEvent> MatchEvents { get; set; }
    public DbSet<MatchStatistic> MatchStatistics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Match>()
            .HasOne(m => m.MatchStatistic)
            .WithOne(s => s.Match)
            .HasForeignKey<MatchStatistic>(s => s.MatchId);
            
        modelBuilder.Entity<Match>()
            .Property(m => m.Status)
            .HasConversion<string>();
    }
}
