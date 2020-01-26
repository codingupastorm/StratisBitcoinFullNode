using CertificateAuthority.Models;
using Microsoft.EntityFrameworkCore;

namespace CertificateAuthority.Database
{
    public class CADbContext : DbContext
    {
        public DbSet<AccountModel> Accounts { get; set; }

        public DbSet<CertificateInfoModel> Certificates { get; set; }

        public DbSet<Permission> Permissions { get; set; }

        private readonly Settings settings;

        public CADbContext(Settings settings)
        {
            this.settings = settings;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source=\"{settings.DatabasePath}\";");

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccountModel>()
                .HasMany(a => a.Permissions)
                .WithOne().HasForeignKey(b => b.AccountModelId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CertificateInfoModel>().ToTable("Certificates");

            base.OnModelCreating(modelBuilder);
        }
    }
}
