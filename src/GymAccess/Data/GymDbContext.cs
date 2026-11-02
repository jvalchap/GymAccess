using GymAccess.Domain;
using Microsoft.EntityFrameworkCore;

namespace GymAccess.Data;

public class GymDbContext : DbContext
{
    public GymDbContext(DbContextOptions<GymDbContext> options) : base(options)
    {
    }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Member>(e =>
        {
            e.Property(m => m.Name).IsRequired().HasMaxLength(200);
            e.Property(m => m.Dni).IsRequired().HasMaxLength(20);
            e.HasIndex(m => m.Dni).IsUnique();
            e.HasMany(m => m.Memberships).WithOne().HasForeignKey(x => x.MemberId);
            e.HasMany(m => m.Debts).WithOne().HasForeignKey(x => x.MemberId);
        });

        modelBuilder.Entity<Debt>(e =>
        {
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<CheckIn>(e =>
        {
            e.Property(c => c.Result).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.DenialReason).HasConversion<string>().HasMaxLength(40);
            e.HasOne<Member>().WithMany().HasForeignKey(c => c.MemberId);
            e.HasIndex(c => new { c.MemberId, c.OccurredAtUtc });
        });
    }
}
