using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TutorApi.Data;
using TutorApi.Models;

namespace TutorApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LessonsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LessonsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/lessons - получить все уроки (для главной страницы)
        [HttpGet]
        public async Task<IActionResult> GetAllLessons()
        {
            var lessons = await _context.Lessons
                .Where(l => l.IsPublished)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new
                {
                    l.Id,
                    l.Title,
                    l.MeetingLink,
                    l.CreatedAt,
                    AuthorName = l.Author != null ? l.Author.Username : "Админ"
                })
                .ToListAsync();

            return Ok(lessons);
        }

        // GET: api/lessons/{id} - получить конкретный урок
        [HttpGet("{id}")]
        public async Task<IActionResult> GetLesson(int id)
        {
            var lesson = await _context.Lessons
                .Include(l => l.Author)
                .FirstOrDefaultAsync(l => l.Id == id && l.IsPublished);

            if (lesson == null)
                return NotFound(new { message = "Урок не найден" });

            return Ok(new
            {
                lesson.Id,
                lesson.Title,
                lesson.Content,
                lesson.MeetingLink,
                lesson.CreatedAt,
                lesson.UpdatedAt,
                AuthorName = lesson.Author?.Username ?? "Админ"
            });
        }

        // POST: api/lessons - создать урок (только админ)
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreateLesson([FromBody] CreateLessonRequest request)
        {
            // Получаем ID текущего админа из токена
            var authorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            var lesson = new Lesson
            {
                Title = request.Title,
                Content = request.Content,
                MeetingLink = request.MeetingLink,
                IsPublished = request.IsPublished,
                AuthorId = authorId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Урок успешно создан",
                lessonId = lesson.Id
            });
        }

        // PUT: api/lessons/{id} - обновить урок (только админ)
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateLesson(int id, [FromBody] UpdateLessonRequest request)
        {
            var lesson = await _context.Lessons.FindAsync(id);
            if (lesson == null)
                return NotFound(new { message = "Урок не найден" });

            lesson.Title = request.Title ?? lesson.Title;
            lesson.Content = request.Content ?? lesson.Content;
            lesson.MeetingLink = request.MeetingLink ?? lesson.MeetingLink;
            lesson.IsPublished = request.IsPublished;
            lesson.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Урок обновлён" });
        }

        // DELETE: api/lessons/{id} - удалить урок (только админ)
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteLesson(int id)
        {
            var lesson = await _context.Lessons.FindAsync(id);
            if (lesson == null)
                return NotFound(new { message = "Урок не найден" });

            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Урок удалён" });
        }
    }

    // Классы для запросов
    public class CreateLessonRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? MeetingLink { get; set; }
        public bool IsPublished { get; set; } = true;
    }

    public class UpdateLessonRequest
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? MeetingLink { get; set; }
        public bool IsPublished { get; set; }
    }
}
