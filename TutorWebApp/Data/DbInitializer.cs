using TutorApi.Models;
using Microsoft.EntityFrameworkCore;

namespace TutorApi.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            try
            {
                Console.WriteLine("🔄 Проверка базы данных...");

                // ВАЖНО: Принудительно создаем ВСЕ таблицы
                // Этот метод гарантированно создаст таблицы, если их нет
                context.Database.EnsureDeleted();
                var created = context.Database.EnsureCreated();

                if (created)
                {
                    Console.WriteLine("✅ База данных и таблицы созданы");
                }
                else
                {
                    Console.WriteLine("📦 База данных уже существует");

                    // Проверяем, существуют ли таблицы
                    try
                    {
                        // Пробуем выполнить простой запрос для проверки существования таблиц
                        var anyUser = context.Users.Any();
                        Console.WriteLine("✅ Таблицы существуют");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Таблицы не найдены: {ex.Message}");
                        Console.WriteLine("🔄 Пробуем пересоздать таблицы...");

                        // Если таблиц нет, удаляем БД и создаем заново
                        context.Database.EnsureDeleted();
                        context.Database.EnsureCreated();
                        Console.WriteLine("✅ Таблицы пересозданы");
                    }
                }

                // Теперь безопасно проверяем и инициализируем данные
                if (!context.Users.Any())
                {
                    Console.WriteLine("📝 Инициализация данных...");

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

                    Console.WriteLine("✅ Админ создан: admin / admin123");
                }
                else
                {
                    Console.WriteLine("📊 Данные уже существуют");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
                Console.WriteLine($"Стек: {ex.StackTrace}");
                throw;
            }
        }
    }
}