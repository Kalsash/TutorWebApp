using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorApi.Data;
using TutorApi.Models;

namespace TutorApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;

        public FilesController(AppDbContext context, IWebHostEnvironment env, IConfiguration configuration)
        {
            _context = context;
            _env = env;
            _configuration = configuration;
        }

        public class UploadFileRequest
        {
            public int? LessonId { get; set; }
        }

        // Загрузка файла
        [HttpPost("upload")]
        [Authorize]
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] int? lessonId)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Файл не выбран" });

            // Проверка расширения
            var allowedExtensions = new[] { ".txt", ".py", ".cs", ".java", ".cpp", ".js", ".html", ".css", ".sql", ".md", ".json", ".xml" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Недопустимый тип файла. Разрешены: " + string.Join(", ", allowedExtensions) });

            // Проверка размера (макс 10 МБ)
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "Файл слишком большой. Максимальный размер: 10 МБ" });

            try
            {
                // Получаем ID пользователя из токена
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

                // Создаем директорию для загрузок, если её нет
                var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // Генерируем уникальное имя файла
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Сохраняем файл
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Сохраняем информацию о файле в БД
                var uploadedFile = new UploadedFile
                {
                    FileName = file.FileName,
                    FilePath = $"/uploads/{uniqueFileName}",
                    FileSize = file.Length,
                    ContentType = file.ContentType,
                    UploadedAt = DateTime.UtcNow,
                    LessonId = lessonId,
                    UserId = userId
                };

                _context.UploadedFiles.Add(uploadedFile);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Файл успешно загружен",
                    file = new
                    {
                        uploadedFile.Id,
                        uploadedFile.FileName,
                        uploadedFile.FilePath,
                        uploadedFile.FileSize,
                        uploadedFile.ContentType,
                        uploadedFile.UploadedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка при загрузке файла", error = ex.Message });
            }
        }

        // Получить все файлы для урока
        [HttpGet("lesson/{lessonId}")]
        public async Task<IActionResult> GetLessonFiles(int lessonId)
        {
            var files = await _context.UploadedFiles
                .Where(f => f.LessonId == lessonId)
                .Select(f => new
                {
                    f.Id,
                    f.FileName,
                    f.FilePath,
                    f.FileSize,
                    f.ContentType,
                    f.UploadedAt,
                    UserName = f.User != null ? f.User.Username : "Unknown"
                })
                .ToListAsync();

            return Ok(files);
        }

        // Скачать файл
        [HttpGet("download/{id}")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            var fileInfo = await _context.UploadedFiles.FindAsync(id);
            if (fileInfo == null)
                return NotFound(new { message = "Файл не найден" });

            var filePath = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", Path.GetFileName(fileInfo.FilePath));

            if (!System.IO.File.Exists(filePath))
                return NotFound(new { message = "Файл не найден на диске" });

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, fileInfo.ContentType ?? "application/octet-stream", fileInfo.FileName);
        }

        // Удалить файл
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteFile(int id)
        {
            var fileInfo = await _context.UploadedFiles.FindAsync(id);
            if (fileInfo == null)
                return NotFound(new { message = "Файл не найден" });

            // Проверяем, что удаляет автор или админ
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);
            var user = await _context.Users.FindAsync(userId);

            if (fileInfo.UserId != userId && user?.Role != "admin")
                return Forbid();

            // Удаляем физический файл
            var filePath = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", Path.GetFileName(fileInfo.FilePath));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            // Удаляем запись из БД
            _context.UploadedFiles.Remove(fileInfo);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Файл удалён" });
        }
    }
}
