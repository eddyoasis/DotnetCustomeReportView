using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<ReportStaff>          ReportStaff           { get; set; }
        public DbSet<UserColumnPreference> UserColumnPreferences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // UserId is the PK (string) — no extra index needed
            modelBuilder.Entity<UserColumnPreference>(e =>
            {
                e.HasKey(p => p.UserId);
                e.Property(p => p.ColumnJson).HasColumnType("nvarchar(max)");
            });

            // ── Seed sample staff data ────────────────────────────────────────
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
