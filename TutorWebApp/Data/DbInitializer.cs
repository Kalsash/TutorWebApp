using TutorApi.Models;

namespace TutorApi.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            context.Database.EnsureCreated();

            // Если нет пользователей, создаём админа
            if (!context.Users.Any())
            {
                var admin = new User
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    Role = "admin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(admin);
                context.SaveChanges();
            }
        }
    }
}
