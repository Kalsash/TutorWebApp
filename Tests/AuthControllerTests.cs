using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TutorApi.Controllers;
using TutorApi.Data;
using TutorApi.Models;
using TutorWebApp.Services;
using Newtonsoft.Json.Linq; // Добавьте этот using

namespace Tests
{
    public class AuthControllerTests
    {
        private readonly AppDbContext _context;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IValidationService> _mockValidationService;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            // Настройка InMemory базы данных
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            // Добавляем тестового пользователя с ВСЕМИ полями
            var user = new User
            {
                Id = 1, // явно задаем Id
                Username = "testuser",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = "user",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            _context.SaveChanges();

            // Более тщательная настройка мока конфигурации
            _mockConfig = new Mock<IConfiguration>();

            // Настраиваем все необходимые ключи
            _mockConfig.Setup(x => x["Jwt:Key"]).Returns("your-test-key-at-least-32-chars-long!!!");
            _mockConfig.Setup(x => x["Jwt:Issuer"]).Returns("TestIssuer");
            _mockConfig.Setup(x => x["Jwt:Audience"]).Returns("TestAudience");
            _mockConfig.Setup(x => x["Jwt:ExpireMinutes"]).Returns("30");

            _mockValidationService = new Mock<IValidationService>();
            _controller = new AuthController(_context, _mockConfig.Object, _mockValidationService.Object);
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsToken()
        {
            // Arrange
            var request = new AuthController.LoginRequest
            {
                Username = "testuser",
                Password = "password123"
            };

            // Act
            var result = await _controller.Login(request);

            // Assert - сначала просто проверяем тип результата
            Assert.IsType<OkObjectResult>(result);

            var okResult = result as OkObjectResult;

            // Проверяем что результат не null
            Assert.NotNull(okResult);
            Assert.NotNull(okResult.Value);

            // Выводим информацию о типе
            var valueType = okResult.Value.GetType();
            Console.WriteLine($"Тип возвращаемого значения: {valueType.FullName}");

            // Выводим все свойства
            var properties = valueType.GetProperties();
            foreach (var prop in properties)
            {
                var propValue = prop.GetValue(okResult.Value);
                Console.WriteLine($"Свойство: {prop.Name} = {propValue} (тип: {prop.PropertyType})");
            }

            // Пытаемся получить значение через рефлексию
            var tokenProperty = valueType.GetProperty("token");
            var roleProperty = valueType.GetProperty("role");

            if (tokenProperty != null)
            {
                var token = tokenProperty.GetValue(okResult.Value);
                Assert.NotNull(token);
                Console.WriteLine($"Найден токен: {token}");
            }
            else
            {
                // Ищем другие возможные имена
                var possibleTokenNames = new[] { "Token", "accessToken", "jwt", "Jwt", "access_token" };
                foreach (var name in possibleTokenNames)
                {
                    var prop = valueType.GetProperty(name);
                    if (prop != null)
                    {
                        var value = prop.GetValue(okResult.Value);
                        Console.WriteLine($"Найдено свойство с именем {name}: {value}");
                    }
                }

                Assert.True(false, "Свойство 'token' не найдено в ответе");
            }

            if (roleProperty != null)
            {
                var role = roleProperty.GetValue(okResult.Value);
                Assert.Equal("user", role.ToString());
            }
            else
            {
                Console.WriteLine("Свойство 'role' не найдено");
            }
        }

        [Fact]
        public async Task Login_InvalidPassword_ReturnsUnauthorized()
        {
            // Arrange
            var request = new AuthController.LoginRequest
            {
                Username = "testuser",
                Password = "wrongpassword"
            };

            // Act
            var result = await _controller.Login(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Login_UserNotFound_ReturnsUnauthorized()
        {
            // Arrange
            var request = new AuthController.LoginRequest
            {
                Username = "nonexistent",
                Password = "password123"
            };

            // Act
            var result = await _controller.Login(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Login_SimpleCheck_JustToSeeResponse()
        {
            // Самый простой тест - просто смотрим что приходит
            var request = new AuthController.LoginRequest
            {
                Username = "testuser",
                Password = "password123"
            };

            var result = await _controller.Login(request);
            var okResult = result as OkObjectResult;

            if (okResult != null && okResult.Value != null)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(okResult.Value);
                Console.WriteLine($"JSON ответ: {json}");
            }
        }
    }
}