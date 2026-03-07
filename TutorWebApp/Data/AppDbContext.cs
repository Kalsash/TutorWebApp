using Microsoft.EntityFrameworkCore;
using TutorApi.Models;

namespace TutorApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Lesson> Lessons { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Уникальность Username
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Связь Lesson -> User (Author)
            modelBuilder.Entity<Lesson>()
                .HasOne(l => l.Author)
                .WithMany()
                .HasForeignKey(l => l.AuthorId)
                .OnDelete(DeleteBehavior.Restrict); // не удалять уроки при удалении автора

            base.OnModelCreating(modelBuilder);
        }
    }
}
