using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Newtonsoft.Json.Linq;
using System.Security.Claims;
using TutorApi.Controllers;
using TutorApi.Data;
using TutorApi.Models;
using TutorWebApp.Services;
using Xunit;

namespace Tests
{
    public class AdminControllerTests
    {
        private readonly AppDbContext _context;
        private readonly Mock<IValidationService> _mockValidationService;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            // Настройка InMemory базы данных
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            // Добавляем тестового админа (для контекста, но контроллер не использует текущего пользователя, кроме авторизации)
            _context.Users.Add(new User
            {
                Id = 1,
                Username = "admin",
                Role = "admin",
                IsActive = true,
                PasswordHash = "hash"
            });
            _context.SaveChanges();

            _mockValidationService = new Mock<IValidationService>();

            _controller = new AdminController(_context, _mockValidationService.Object);

            // Настройка контекста контроллера с fake пользователем (роль admin)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Role, "admin")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        // Вспомогательный метод для создания валидации (успех)
        private void SetupValidationSuccess()
        {
            _mockValidationService.Setup(v => v.ValidateUsername(It.IsAny<string>()))
                .Returns(new ValidationResult { IsValid = true });
            _mockValidationService.Setup(v => v.ValidateNewPassword(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new ValidationResult { IsValid = true });
            _mockValidationService.Setup(v => v.ValidateFirstName(It.IsAny<string>()))
                .Returns(new ValidationResult { IsValid = true });
            _mockValidationService.Setup(v => v.ValidateLastName(It.IsAny<string>()))
                .Returns(new ValidationResult { IsValid = true });
        }

        // -------------- CreateUser tests --------------

        [Fact]
        public async Task CreateUser_ValidRequest_ReturnsOkWithUser()
        {
            // Arrange
            SetupValidationSuccess();
            var request = new AdminController.CreateUserRequest
            {
                Username = "newstudent",
                Password = "ValidPass123!",
                FirstName = "Иван",
                LastName = "Иванов"
            };

            // Act
            var result = await _controller.CreateUser(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JObject.FromObject(okResult.Value);

            Assert.Equal("Ученик успешно создан", json["message"].ToString());
            Assert.NotNull(json["user"]);
            // Исправлено: свойство Username (с большой буквы)
            Assert.Equal("newstudent", json["user"]["Username"].ToString());

            // Проверяем, что пользователь реально добавился в БД
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Username == "newstudent");
            Assert.NotNull(userInDb);
            Assert.Equal("Иван", userInDb.FirstName);
            Assert.Equal("Иванов", userInDb.LastName);
        }

        [Fact]
        public async Task CreateUser_InvalidUsername_ReturnsBadRequest()
        {
            // Arrange
            _mockValidationService.Setup(v => v.ValidateUsername(It.IsAny<string>()))
                .Returns(new ValidationResult { IsValid = false, ErrorMessage = "Invalid username" });
            var request = new AdminController.CreateUserRequest
            {
                Username = "bad",
                Password = "ValidPass123!",
            };

            // Act
            var result = await _controller.CreateUser(request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var json = JObject.FromObject(badRequest.Value);
            Assert.Equal("Invalid username", json["message"].ToString());
        }

        [Fact]
        public async Task CreateUser_InvalidPassword_ReturnsBadRequest()
        {
            // Arrange
            _mockValidationService.Setup(v => v.ValidateUsername(It.IsAny<string>()))
                .Returns(new ValidationResult { IsValid = true });
            _mockValidationService.Setup(v => v.ValidateNewPassword(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new ValidationResult { IsValid = false, ErrorMessage = "Invalid password" });
            var request = new AdminController.CreateUserRequest
            {
                Username = "validuser",
                Password = "weak",
            };

            // Act
            var result = await _controller.CreateUser(request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var json = JObject.FromObject(badRequest.Value);
            Assert.Equal("Invalid password", json["message"].ToString());
        }

        [Fact]
        public async Task CreateUser_UsernameAlreadyExists_ReturnsBadRequest()
        {
            // Arrange
            SetupValidationSuccess();
            // Предварительно добавляем пользователя
            _context.Users.Add(new User
            {
                Username = "existing",
                PasswordHash = "hash",
                Role = "user"
            });
            await _context.SaveChangesAsync();

            var request = new AdminController.CreateUserRequest
            {
                Username = "existing",
                Password = "ValidPass123!"
            };

            // Act
            var result = await _controller.CreateUser(request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var json = JObject.FromObject(badRequest.Value);
            Assert.Equal("Пользователь с таким логином уже существует", json["message"].ToString());
        }

        // -------------- UpdateUser tests --------------

        [Fact]
        public async Task UpdateUser_ValidRequest_UpdatesUser()
        {
            // Arrange
            SetupValidationSuccess();
            var user = new User
            {
                Id = 10,
                Username = "oldname",
                FirstName = "Old",
                LastName = "User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("oldpass"),
                Role = "user",
                IsActive = true
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new AdminController.UpdateUserRequest
            {
                Username = "newname",
                FirstName = "New",
                LastName = "Name",
                Password = "NewPass123!"
            };

            // Act
            var result = await _controller.UpdateUser(10, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JObject.FromObject(okResult.Value);
            Assert.Equal("Данные ученика успешно обновлены", json["message"].ToString());

            var updatedUser = await _context.Users.FindAsync(10);
            Assert.Equal("newname", updatedUser.Username);
            Assert.Equal("New", updatedUser.FirstName);
            Assert.Equal("Name", updatedUser.LastName);
            // Пароль должен измениться
            Assert.True(BCrypt.Net.BCrypt.Verify("NewPass123!", updatedUser.PasswordHash));
        }

        [Fact]
        public async Task UpdateUser_UserNotFound_ReturnsNotFound()
        {
            // Arrange
            SetupValidationSuccess();
            var request = new AdminController.UpdateUserRequest
            {
                Username = "newname"
            };

            // Act
            var result = await _controller.UpdateUser(999, request);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var json = JObject.FromObject(notFound.Value);
            Assert.Equal("Ученик не найден", json["message"].ToString());
        }

        [Fact]
        public async Task UpdateUser_UsernameConflict_ReturnsBadRequest()
        {
            // Arrange
            SetupValidationSuccess();
            // Используем Id, не конфликтующие с админом (Id=1)
            _context.Users.Add(new User { Id = 100, Username = "target", Role = "user" });
            _context.Users.Add(new User { Id = 101, Username = "conflict", Role = "user" });
            await _context.SaveChangesAsync();

            // Очищаем трекер, чтобы избежать побочных эффектов
            _context.ChangeTracker.Clear();

            var request = new AdminController.UpdateUserRequest
            {
                Username = "conflict" // пробуем сменить имя target на conflict
            };

            // Act
            var result = await _controller.UpdateUser(100, request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var json = JObject.FromObject(badRequest.Value);
            Assert.Equal("Пользователь с таким логином уже существует", json["message"].ToString());
        }

        // -------------- DeleteUser tests --------------

        [Fact]
        public async Task DeleteUser_ExistingUser_RemovesUser()
        {
            // Arrange
            var user = new User { Id = 5, Username = "todelete", Role = "user" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.DeleteUser(5);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JObject.FromObject(okResult.Value);
            Assert.Equal("Ученик успешно удалён", json["message"].ToString());
            Assert.Null(await _context.Users.FindAsync(5));
        }

        [Fact]
        public async Task DeleteUser_NotFound_ReturnsNotFound()
        {
            // Act
            var result = await _controller.DeleteUser(999);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var json = JObject.FromObject(notFound.Value);
            Assert.Equal("Ученик не найден", json["message"].ToString());
        }

        // -------------- GetAllUsers tests --------------

        [Fact]
        public async Task GetAllUsers_ReturnsOnlyUsers()
        {
            // Arrange
            _context.Users.AddRange(
                new User { Id = 2, Username = "user1", Role = "user", FirstName = "A", LastName = "B", IsActive = true, CreatedAt = DateTime.UtcNow },
                new User { Id = 3, Username = "user2", Role = "user", FirstName = "C", LastName = "D", IsActive = false, CreatedAt = DateTime.UtcNow },
                new User { Id = 4, Username = "admin2", Role = "admin" } // не должен попасть
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetAllUsers();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var users = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value);
            Assert.Equal(2, users.Count());
        }

        // -------------- GetUser tests --------------

        [Fact]
        public async Task GetUser_ExistingUser_ReturnsUser()
        {
            // Arrange
            _context.Users.Add(new User
            {
                Id = 20,
                Username = "specific",
                Role = "user",
                FirstName = "Test",
                LastName = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetUser(20);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JObject.FromObject(okResult.Value);
            // Исправлено: Id и Username с большой буквы
            Assert.Equal(20, json["Id"].Value<int>());
            Assert.Equal("specific", json["Username"].ToString());
        }

        [Fact]
        public async Task GetUser_NotFound_ReturnsNotFound()
        {
            // Act
            var result = await _controller.GetUser(999);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var json = JObject.FromObject(notFound.Value);
            Assert.Equal("Ученик не найден", json["message"].ToString());
        }

        // -------------- Block/Unblock tests --------------

        [Fact]
        public async Task BlockUser_SetsIsActiveFalse()
        {
            // Arrange
            var user = new User { Id = 30, Username = "activeuser", Role = "user", IsActive = true };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.BlockUser(30);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var blockedUser = await _context.Users.FindAsync(30);
            Assert.False(blockedUser.IsActive);
        }

        [Fact]
        public async Task UnblockUser_SetsIsActiveTrue()
        {
            // Arrange
            var user = new User { Id = 31, Username = "blockeduser", Role = "user", IsActive = false };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.UnblockUser(31);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var unblockedUser = await _context.Users.FindAsync(31);
            Assert.True(unblockedUser.IsActive);
        }

        [Fact]
        public async Task BlockUser_NotFound_ReturnsNotFound()
        {
            // Act
            var result = await _controller.BlockUser(999);
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var json = JObject.FromObject(notFound.Value);
            Assert.Equal("Ученик не найден", json["message"].ToString());
        }

        // -------------- ResetPassword tests --------------

        [Fact]
        public async Task ResetPassword_ValidPassword_UpdatesPassword()
        {
            // Arrange
            var user = new User
            {
                Id = 40,
                Username = "resetuser",
                Role = "user",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("oldpass")
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _mockValidationService.Setup(v => v.ValidateNewPassword("NewPass123!", true))
                .Returns(new ValidationResult { IsValid = true });

            var request = new AdminController.ResetPasswordRequest { NewPassword = "NewPass123!" };

            // Act
            var result = await _controller.ResetPassword(40, request);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var updatedUser = await _context.Users.FindAsync(40);
            Assert.True(BCrypt.Net.BCrypt.Verify("NewPass123!", updatedUser.PasswordHash));
        }

        [Fact]
        public async Task ResetPassword_InvalidPassword_ReturnsBadRequest()
        {
            // Arrange
            var user = new User { Id = 41, Username = "resetuser2", Role = "user" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _mockValidationService.Setup(v => v.ValidateNewPassword("weak", true))
                .Returns(new ValidationResult { IsValid = false, ErrorMessage = "Password too weak" });

            var request = new AdminController.ResetPasswordRequest { NewPassword = "weak" };

            // Act
            var result = await _controller.ResetPassword(41, request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var json = JObject.FromObject(badRequest.Value);
            Assert.Equal("Password too weak", json["message"].ToString());
        }

        [Fact]
        public async Task ResetPassword_UserNotFound_ReturnsNotFound()
        {
            // Arrange
            _mockValidationService.Setup(v => v.ValidateNewPassword(It.IsAny<string>(), true))
                .Returns(new ValidationResult { IsValid = true });

            var request = new AdminController.ResetPasswordRequest { NewPassword = "NewPass123!" };

            // Act
            var result = await _controller.ResetPassword(999, request);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var json = JObject.FromObject(notFound.Value);
            Assert.Equal("Ученик не найден", json["message"].ToString());
        }
    }
}