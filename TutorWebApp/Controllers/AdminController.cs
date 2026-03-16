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
        }

        /// <summary>
        /// Создать нового ученика (только админ)
        /// </summary>
        [HttpPost("createuser")]
        public async Task<IActionResult> CreateUser(CreateUserRequest request)
        {
            // Валидация username
            var usernameValidation = _validationService.ValidateUsername(request.Username);
            if (!usernameValidation.IsValid)
                return BadRequest(new { message = usernameValidation.ErrorMessage });

            // Валидация пароля (при создании пользователя требуем сложный пароль)
            var passwordValidation = _validationService.ValidateNewPassword(request.Password, isReset: false);
            if (!passwordValidation.IsValid)
                return BadRequest(new { message = passwordValidation.ErrorMessage });

            // Проверяем, не занят ли логин
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (existingUser != null)
                return BadRequest(new { message = "Пользователь с таким логином уже существует" });

            var user = new User
            {
                Username = request.Username,
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
                    user.Role,
                    user.IsActive,
                    user.CreatedAt
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
                .Where(u => u.Role == "user") // Только ученики, без админов
                .Select(u => new
                {
                    u.Id,
                    u.Username,
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
