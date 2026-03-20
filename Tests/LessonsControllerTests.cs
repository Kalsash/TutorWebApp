using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Security.Claims;
using TutorApi.Controllers;
using TutorApi.Data;
using TutorApi.Models;
using Xunit;

namespace Tests
{
    public class LessonsControllerTests
    {
        private readonly AppDbContext _context;
        private readonly LessonsController _controller;

        public LessonsControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            _context.Users.Add(new User
            {
                Id = 1,
                Username = "admin",
                Role = "admin",
                IsActive = true,
                PasswordHash = "hash"
            });
            _context.SaveChanges();

            _controller = new LessonsController(_context);
        }

        private void SetAdminUser()
        {
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

        [Fact]
        public async Task GetAllLessons_ReturnsOnlyPublishedLessons()
        {
            _context.Lessons.AddRange(
                new Lesson { Id = 1, Title = "Published 1", Content = "Content", IsPublished = true, AuthorId = 1, CreatedAt = DateTime.UtcNow.AddDays(-1) },
                new Lesson { Id = 2, Title = "Published 2", Content = "Content", IsPublished = true, AuthorId = 1, CreatedAt = DateTime.UtcNow },
                new Lesson { Id = 3, Title = "Unpublished", Content = "Content", IsPublished = false, AuthorId = 1, CreatedAt = DateTime.UtcNow }
            );
            await _context.SaveChangesAsync();

            var result = await _controller.GetAllLessons();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JArray.FromObject(okResult.Value);
            Assert.Equal(2, json.Count);
            Assert.Equal("Published 2", json[0]["Title"].ToString());
            Assert.Equal("Published 1", json[1]["Title"].ToString());
        }

        [Fact]
        public async Task GetLesson_ExistingPublishedLesson_ReturnsLesson()
        {
            _context.Lessons.Add(new Lesson
            {
                Id = 10,
                Title = "Test Lesson",
                Content = "Content",
                IsPublished = true,
                AuthorId = 1,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var result = await _controller.GetLesson(10);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JObject.FromObject(okResult.Value);
            Assert.Equal(10, json["Id"].Value<int>());
            Assert.Equal("Test Lesson", json["Title"].ToString());
            Assert.Equal("admin", json["AuthorName"].ToString());
        }

        [Fact]
        public async Task GetLesson_ExistingUnpublishedLesson_ReturnsNotFound()
        {
            _context.Lessons.Add(new Lesson
            {
                Id = 11,
                Title = "Unpublished Lesson",
                Content = "Content",
                IsPublished = false,
                AuthorId = 1
            });
            await _context.SaveChangesAsync();

            var result = await _controller.GetLesson(11);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var json = JObject.FromObject(notFound.Value);
            Assert.Equal("Урок не найден", json["message"].ToString());
        }

        [Fact]
        public async Task GetLesson_NonexistentLesson_ReturnsNotFound()
        {
            var result = await _controller.GetLesson(999);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var json = JObject.FromObject(notFound.Value);
            Assert.Equal("Урок не найден", json["message"].ToString());
        }

        [Fact]
        public async Task CreateLesson_AsAdmin_ReturnsOkWithLessonId()
        {
            SetAdminUser();
            var request = new CreateLessonRequest
            {
                Title = "New Lesson",
                Content = "Lesson content",
                MeetingLink = "https://meet.example.com",
                IsPublished = true
            };

            var result = await _controller.CreateLesson(request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JObject.FromObject(okResult.Value);
            Assert.Equal("Урок успешно создан", json["message"].ToString());
            Assert.NotNull(json["lessonId"]);

            var lessonId = json["lessonId"].Value<int>();
            var lesson = await _context.Lessons.FindAsync(lessonId);
            Assert.NotNull(lesson);
            Assert.Equal("New Lesson", lesson.Title);
            Assert.Equal(1, lesson.AuthorId);
        }

        [Fact]
        public async Task UpdateLesson_AsAdmin_UpdatesLesson()
        {
            SetAdminUser();
            var lesson = new Lesson
            {
                Id = 20,
                Title = "Old Title",
                Content = "Old Content",
                MeetingLink = "oldlink",
                IsPublished = true,
                AuthorId = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            var request = new UpdateLessonRequest
            {
                Title = "New Title",
                Content = "New Content",
                MeetingLink = "newlink",
                IsPublished = false
            };

            var result = await _controller.UpdateLesson(20, request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JObject.FromObject(okResult.Value);
            Assert.Equal("Урок обновлён", json["message"].ToString());

            var updated = await _context.Lessons.FindAsync(20);
            Assert.Equal("New Title", updated.Title);
            Assert.Equal("New Content", updated.Content);
            Assert.Equal("newlink", updated.MeetingLink);
            Assert.False(updated.IsPublished);
            Assert.NotNull(updated.UpdatedAt);
        }

        [Fact]
        public async Task UpdateLesson_PartialUpdate_UpdatesOnlyProvidedFields()
        {
            SetAdminUser();
            var lesson = new Lesson
            {
                Id = 21,
                Title = "Original Title",
                Content = "Original Content",
                MeetingLink = "original",
                IsPublished = true,
                AuthorId = 1,
                CreatedAt = DateTime.UtcNow
            };
            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            var request = new UpdateLessonRequest
            {
                Title = "Only Title Changed",
                IsPublished = false
            };

            var result = await _controller.UpdateLesson(21, request);

            Assert.IsType<OkObjectResult>(result);

            var updated = await _context.Lessons.FindAsync(21);
            Assert.Equal("Only Title Changed", updated.Title);
            Assert.Equal("Original Content", updated.Content);
            Assert.Equal("original", updated.MeetingLink);
            Assert.False(updated.IsPublished);
        }

        [Fact]
        public async Task UpdateLesson_NonexistentLesson_ReturnsNotFound()
        {
            SetAdminUser();
            var request = new UpdateLessonRequest { Title = "New" };

            var result = await _controller.UpdateLesson(999, request);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var json = JObject.FromObject(notFound.Value);
            Assert.Equal("Урок не найден", json["message"].ToString());
        }

        [Fact]
        public async Task DeleteLesson_AsAdmin_RemovesLesson()
        {
            SetAdminUser();
            var lesson = new Lesson { Id = 30, Title = "ToDelete", AuthorId = 1 };
            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            var result = await _controller.DeleteLesson(30);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JObject.FromObject(okResult.Value);
            Assert.Equal("Урок удалён", json["message"].ToString());

            var deleted = await _context.Lessons.FindAsync(30);
            Assert.Null(deleted);
        }

        [Fact]
        public async Task DeleteLesson_NonexistentLesson_ReturnsNotFound()
        {
            SetAdminUser();
            var result = await _controller.DeleteLesson(999);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var json = JObject.FromObject(notFound.Value);
            Assert.Equal("Урок не найден", json["message"].ToString());
        }
    }
}