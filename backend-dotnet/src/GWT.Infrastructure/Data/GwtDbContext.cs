using GWT.Domain.Entities;
using GWT.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GWT.Infrastructure.Data;

public class GwtDbContext : DbContext
{
    public GwtDbContext(DbContextOptions<GwtDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<FundMeta> FundMetas => Set<FundMeta>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<NavHistory> NavHistories => Set<NavHistory>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── User ──────────────────────────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
            e.Property(u => u.Name).HasColumnName("name").HasMaxLength(100);
            e.Property(u => u.PasswordHash).HasColumnName("password_hash");
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
            e.HasIndex(u => u.Email).IsUnique();
        });

        // ── FundMeta ──────────────────────────────────────────────────────
        mb.Entity<FundMeta>(e =>
        {
            e.ToTable("fund_meta");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasColumnName("id").HasMaxLength(50);
            e.Property(f => f.Region).HasColumnName("region")
                .HasConversion<string>()
                .HasMaxLength(10);
            e.Property(f => f.Name).HasColumnName("name").HasMaxLength(500).IsRequired();
            e.Property(f => f.Amc).HasColumnName("amc").HasMaxLength(200).IsRequired();
            e.Property(f => f.Ticker).HasColumnName("ticker").HasMaxLength(50).IsRequired();
            e.Property(f => f.SchemeCode).HasColumnName("scheme_code").HasMaxLength(20);
            e.Property(f => f.Isin).HasColumnName("isin").HasMaxLength(20);
            e.Property(f => f.Category).HasColumnName("category").HasMaxLength(100);
            e.Property(f => f.Timezone).HasColumnName("timezone").HasMaxLength(50);
            e.Property(f => f.UpdatedAt).HasColumnName("updated_at");
            e.Property(f => f.LatestNav).HasColumnName("latest_nav").HasColumnType("numeric(18,6)");
            e.Property(f => f.NavDate).HasColumnName("nav_date");
            e.HasIndex(f => f.Ticker).IsUnique();
            e.HasIndex(f => new { f.Region, f.Name, f.Amc });
        });

        // ── Holding ───────────────────────────────────────────────────────
        mb.Entity<Holding>(e =>
        {
            e.ToTable("holdings");
            e.HasKey(h => h.Id);
            e.Property(h => h.Id).HasColumnName("id");
            e.Property(h => h.UserId).HasColumnName("user_id");
            e.Property(h => h.FundId).HasColumnName("fund_id").HasMaxLength(50);
            e.Property(h => h.Units).HasColumnName("units").HasColumnType("numeric(18,6)");
            e.Property(h => h.AvgCost).HasColumnName("avg_cost").HasColumnType("numeric(18,6)");
            e.Property(h => h.PurchaseAt).HasColumnName("purchase_at");
            e.Property(h => h.CreatedAt).HasColumnName("created_at");
            e.Property(h => h.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(h => h.User)
                .WithMany(u => u.Holdings)
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(h => h.Fund)
                .WithMany(f => f.Holdings)
                .HasForeignKey(h => h.FundId)
                .OnDelete(DeleteBehavior.Restrict);

            // Enforces auto-consolidation: one row per (user, fund)
            e.HasIndex(h => new { h.UserId, h.FundId }).IsUnique();
            e.HasIndex(h => h.UserId);
        });

        // ── NavHistory ────────────────────────────────────────────────────
        mb.Entity<NavHistory>(e =>
        {
            e.ToTable("nav_history");
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).HasColumnName("id");
            e.Property(n => n.FundId).HasColumnName("fund_id").HasMaxLength(50);
            e.Property(n => n.Nav).HasColumnName("nav").HasColumnType("numeric(18,6)");
            e.Property(n => n.NavDate).HasColumnName("nav_date");

            e.HasOne(n => n.Fund)
                .WithMany(f => f.NavHistories)
                .HasForeignKey(n => n.FundId)
                .OnDelete(DeleteBehavior.Cascade);

            // One entry per fund per day
            e.HasIndex(n => new { n.FundId, n.NavDate }).IsUnique();
        });
    }
}
