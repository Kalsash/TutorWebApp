using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorApi.Data;
using TutorApi.Models;
using Microsoft.EntityFrameworkCore;
using TutorWebApp.Services;

namespace TutorApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IValidationService _validationService;

        public AdminController(AppDbContext context, IValidationService validationService)
        {
            _context = context;
            _validationService = validationService;
        }

        public class CreateUserRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
        }

        public class UpdateUserRequest
        {
            public string Username { get; set; } = string.Empty;
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Password { get; set; } // Опционально, если нужно сменить пароль
        }

        /// <summary>
        /// Создать нового ученика
        /// </summary>
        [HttpPost("createuser")]
        public async Task<IActionResult> CreateUser(CreateUserRequest request)
        {
            // Валидация username
            var usernameValidation = _validationService.ValidateUsername(request.Username);
            if (!usernameValidation.IsValid)
                return BadRequest(new { message = usernameValidation.ErrorMessage });

            // Валидация пароля
            var passwordValidation = _validationService.ValidateNewPassword(request.Password, isReset: false);
            if (!passwordValidation.IsValid)
                return BadRequest(new { message = passwordValidation.ErrorMessage });

            // Валидация имени (если указано)
            if (!string.IsNullOrEmpty(request.FirstName))
            {
                var firstNameValidation = _validationService.ValidateFirstName(request.FirstName);
                if (!firstNameValidation.IsValid)
                    return BadRequest(new { message = firstNameValidation.ErrorMessage });
            }

            // Валидация фамилии (если указана)
            if (!string.IsNullOrEmpty(request.LastName))
            {
                var lastNameValidation = _validationService.ValidateLastName(request.LastName);
                if (!lastNameValidation.IsValid)
                    return BadRequest(new { message = lastNameValidation.ErrorMessage });
            }

            // Проверяем, не занят ли логин
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (existingUser != null)
                return BadRequest(new { message = "Пользователь с таким логином уже существует" });

            var user = new User
            {
                Username = request.Username,
                FirstName = request.FirstName?.Trim(),
                LastName = request.LastName?.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = "user",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Ученик успешно создан",
                user = new
                {
                    user.Id,
                    user.Username,
                    user.FirstName,
                    user.LastName,
                    user.FullName,
                    user.Role,
                    user.IsActive,
                    user.CreatedAt
                }
            });
        }

        /// <summary>
        /// Редактировать ученика (логин, имя, фамилия, пароль)
        /// </summary>
        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, UpdateUserRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == "user");

            if (user == null)
                return NotFound(new { message = "Ученик не найден" });

            // Валидация нового логина, если он изменяется
            if (user.Username != request.Username)
            {
                var usernameValidation = _validationService.ValidateUsername(request.Username);
                if (!usernameValidation.IsValid)
                    return BadRequest(new { message = usernameValidation.ErrorMessage });

                // Проверяем, не занят ли новый логин другим пользователем
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username && u.Id != id);

                if (existingUser != null)
                    return BadRequest(new { message = "Пользователь с таким логином уже существует" });
            }

            // Валидация имени (если указано и изменилось)
            if (!string.IsNullOrEmpty(request.FirstName) && user.FirstName != request.FirstName?.Trim())
            {
                var firstNameValidation = _validationService.ValidateFirstName(request.FirstName);
                if (!firstNameValidation.IsValid)
                    return BadRequest(new { message = firstNameValidation.ErrorMessage });
            }

            // Валидация фамилии (если указана и изменилась)
            if (!string.IsNullOrEmpty(request.LastName) && user.LastName != request.LastName?.Trim())
            {
                var lastNameValidation = _validationService.ValidateLastName(request.LastName);
                if (!lastNameValidation.IsValid)
                    return BadRequest(new { message = lastNameValidation.ErrorMessage });
            }

            // Обновляем поля
            user.Username = request.Username;
            user.FirstName = request.FirstName?.Trim();
            user.LastName = request.LastName?.Trim();

            // Если передан новый пароль, обновляем его
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                var passwordValidation = _validationService.ValidateNewPassword(request.Password, isReset: true);
                if (!passwordValidation.IsValid)
                    return BadRequest(new { message = passwordValidation.ErrorMessage });

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Данные ученика успешно обновлены",
                user = new
                {
                    user.Id,
                    user.Username,
                    user.FirstName,
                    user.LastName,
                    user.FullName,
                    user.Role,
                    user.IsActive,
                    user.CreatedAt,
                    user.LastLoginAt
                }
            });
        }

        /// <summary>
        /// Получить список всех учеников (только админ)
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Where(u => u.Role == "user")
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.FirstName,
                    u.LastName,
                    FullName = u.LastName + " " + u.FirstName,
                    u.IsActive,
                    u.CreatedAt,
                    u.LastLoginAt
                })
                .ToListAsync();

            return Ok(users);
        }

        /// <summary>
        /// Получить информацию о конкретном ученике (только админ)
        /// </summary>
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id && u.Role == "user")
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.FirstName,
                    u.LastName,
                    FullName = u.FullName,
                    u.IsActive,
                    u.CreatedAt,
                    u.LastLoginAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new { message = "Ученик не найден" });

            return Ok(user);
        }

        /// <summary>
        /// Удалить ученика (только админ)
        /// </summary>
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == "user");

            if (user == null)
                return NotFound(new { message = "Ученик не найден" });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ученик успешно удалён" });
        }

        /// <summary>
        /// Заблокировать ученика (мягкое удаление)
        /// </summary>
        [HttpPatch("users/{id}/block")]
        public async Task<IActionResult> BlockUser(int id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == "user");

            if (user == null)
                return NotFound(new { message = "Ученик не найден" });

            user.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ученик заблокирован" });
        }

        /// <summary>
        /// Разблокировать ученика
        /// </summary>
        [HttpPatch("users/{id}/unblock")]
        public async Task<IActionResult> UnblockUser(int id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == "user");

            if (user == null)
                return NotFound(new { message = "Ученик не найден" });

            user.IsActive = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ученик разблокирован" });
        }

        /// <summary>
        /// Сбросить пароль ученика (админ задаёт новый пароль)
        /// </summary>
        [HttpPost("users/{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
        {
            // Валидация пароля с флагом isReset = true (менее строгие требования)
            var passwordValidation = _validationService.ValidateNewPassword(request.NewPassword, isReset: true);
            if (!passwordValidation.IsValid)
                return BadRequest(new { message = passwordValidation.ErrorMessage });

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == "user");

            if (user == null)
                return NotFound(new { message = "Ученик не найден" });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Пароль успешно сброшен" });
        }

        public class ResetPasswordRequest
        {
            public string NewPassword { get; set; } = string.Empty;
        }
    }
}