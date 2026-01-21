using Assignment_2_242942m.Models;
using Microsoft.EntityFrameworkCore;

namespace Assignment_2_242942m.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

        public DbSet<Member> Members => Set<Member>();
        public DbSet<SessionTicket> SessionTickets => Set<SessionTicket>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Member>().HasIndex(m => m.Email).IsUnique();
        }
    }
}
