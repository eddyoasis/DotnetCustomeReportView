using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<ReportStaff>          ReportStaff           { get; set; }
        public DbSet<ReportDefinition>     ReportDefinitions     { get; set; }
        public DbSet<ReportColumn>         ReportColumns         { get; set; }
        public DbSet<UserColumnPreference> UserColumnPreferences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── UserColumnPreference: composite PK (UserId + ReportDefinitionId + ClientCode) ──
            modelBuilder.Entity<UserColumnPreference>(e =>
            {
                e.HasKey(p => new { p.UserId, p.ReportDefinitionId, p.ClientCode });
                e.Property(p => p.ClientCode)
                    .HasMaxLength(128)
                    .HasDefaultValue(string.Empty);
                e.Property(p => p.ColumnJson).HasColumnType("nvarchar(max)");
                e.HasIndex(p => p.UserId);
            });

            // ── ReportDefinition ──────────────────────────────────────────────
            modelBuilder.Entity<ReportDefinition>()
                .HasMany(r => r.Columns)
                .WithOne(c => c.Report)
                .HasForeignKey(c => c.ReportDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── Seed: Staff Report ────────────────────────────────────────────
            modelBuilder.Entity<ReportDefinition>().HasData(
                new ReportDefinition { Id = 1, ReportName = "Staff Report", SourceTable = "ReportStaff", SourceSP = null, IsActive = true }
            );

            modelBuilder.Entity<ReportColumn>().HasData(
                new ReportColumn { Id = 1, ReportDefinitionId = 1, PropertyName = "Id",   DefaultLabel = "ID",   DisplayOrder = 1 },
                new ReportColumn { Id = 2, ReportDefinitionId = 1, PropertyName = "Name", DefaultLabel = "Name", DisplayOrder = 2 },
                new ReportColumn { Id = 3, ReportDefinitionId = 1, PropertyName = "Age",  DefaultLabel = "Age",  DisplayOrder = 3 }
            );

            // ── Seed: Staff data ──────────────────────────────────────────────
            modelBuilder.Entity<ReportStaff>().HasData(
                new ReportStaff { Id = 1, Name = "Alice Johnson",  Age = 30 },
                new ReportStaff { Id = 2, Name = "Bob Smith",      Age = 45 },
                new ReportStaff { Id = 3, Name = "Carol Williams", Age = 28 },
                new ReportStaff { Id = 4, Name = "David Brown",    Age = 52 },
                new ReportStaff { Id = 5, Name = "Eve Davis",      Age = 35 }
            );
        }
    }
}
