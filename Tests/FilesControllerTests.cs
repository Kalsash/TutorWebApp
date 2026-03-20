using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json.Linq;
using System.Security.Claims;
using TutorApi.Controllers;
using TutorApi.Data;
using TutorApi.Models;
using Xunit;

namespace Tests
{
    public class FilesControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly FilesController _controller;
        private readonly string _testUploadsPath;

        public FilesControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            _context.Users.Add(new User
            {
                Id = 1,
                Username = "user1",
                Role = "user",
                IsActive = true,
                PasswordHash = "hash"
            });
            _context.SaveChanges();

            _testUploadsPath = Path.Combine(Path.GetTempPath(), "TestUploads_" + Guid.NewGuid().ToString());
            _mockEnv = new Mock<IWebHostEnvironment>();
            _mockEnv.Setup(e => e.WebRootPath).Returns(_testUploadsPath);
            _mockEnv.Setup(e => e.ContentRootPath).Returns(Directory.GetCurrentDirectory());

            _mockConfig = new Mock<IConfiguration>();

            _controller = new FilesController(_context, _mockEnv.Object, _mockConfig.Object);
        }

        private void SetAuthUser(int userId = 1)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
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

        public void Dispose()
        {
            if (Directory.Exists(_testUploadsPath))
                Directory.Delete(_testUploadsPath, true);
        }

        // -------------------- UploadFile tests --------------------

        [Fact]
        public async Task UploadFile_ValidFile_ReturnsOkWithFileInfo()
        {
            SetAuthUser();
            var content = "test file content";
            var fileName = "test.cs";
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(content);
            writer.Flush();
            stream.Position = 0;

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.Length).Returns(stream.Length);
            fileMock.Setup(f => f.ContentType).Returns("text/plain");
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

            var result = await _controller.UploadFile(fileMock.Object, null);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JObject.FromObject(okResult.Value);
            Assert.Equal("Файл успешно загружен", json["message"].ToString());
            Assert.NotNull(json["file"]);
            Assert.Equal(fileName, json["file"]["FileName"].ToString());

            var fileInDb = await _context.UploadedFiles.FirstOrDefaultAsync();
            Assert.NotNull(fileInDb);
            Assert.Equal(fileName, fileInDb.FileName);
            Assert.Equal(1, fileInDb.UserId);

            var physicalPath = Path.Combine(_testUploadsPath, "uploads", Path.GetFileName(fileInDb.FilePath));
            Assert.True(File.Exists(physicalPath));
        }

        [Fact]
        public async Task UploadFile_NoFile_ReturnsBadRequest()
        {
            SetAuthUser();
            var result = await _controller.UploadFile(null, null);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var json = JObject.FromObject(badRequest.Value);
            Assert.Equal("Файл не выбран", json["message"].ToString());
        }

        [Theory]
        [InlineData(".exe")]
        [InlineData(".dll")]
        [InlineData(".jpg")]
        public async Task UploadFile_DisallowedExtension_ReturnsBadRequest(string extension)
        {
            SetAuthUser();
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns($"file{extension}");
            fileMock.Setup(f => f.Length).Returns(100);
            var result = await _controller.UploadFile(fileMock.Object, null);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var json = JObject.FromObject(badRequest.Value);
            Assert.Contains("Недопустимый тип файла", json["message"].ToString());
        }

        [Fact]
        public async Task UploadFile_FileTooLarge_ReturnsBadRequest()
        {
            SetAuthUser();
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("large.cs");
            fileMock.Setup(f => f.Length).Returns(11 * 1024 * 1024);
            var result = await _controller.UploadFile(fileMock.Object, null);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var json = JObject.FromObject(badRequest.Value);
            Assert.Equal("Файл слишком большой. Максимальный размер: 10 МБ", json["message"].ToString());
        }

        // -------------------- GetLessonFiles tests --------------------

        [Fact]
        public async Task GetLessonFiles_ReturnsFilesForLesson()
        {
            var lessonId = 5;
            _context.UploadedFiles.AddRange(
                new UploadedFile { Id = 1, FileName = "file1.txt", FilePath = "/uploads/1.txt", LessonId = lessonId, UserId = 1 },
                new UploadedFile { Id = 2, FileName = "file2.txt", FilePath = "/uploads/2.txt", LessonId = lessonId, UserId = 1 },
                new UploadedFile { Id = 3, FileName = "file3.txt", FilePath = "/uploads/3.txt", LessonId = 99, UserId = 1 }
            );
            await _context.SaveChangesAsync();

            var result = await _controller.GetLessonFiles(lessonId);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JArray.FromObject(okResult.Value);
            Assert.Equal(2, json.Count);
            Assert.Equal("file1.txt", json[0]["FileName"].ToString());
            Assert.Equal("file2.txt", json[1]["FileName"].ToString());
        }

        [Fact]
        public async Task GetLessonFiles_NoFiles_ReturnsEmptyArray()
        {
            var result = await _controller.GetLessonFiles(999);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JArray.FromObject(okResult.Value);
            Assert.Empty(json);
        }

        // -------------------- DownloadFile tests --------------------

        [Fact]
        public async Task DownloadFile_ExistingFile_ReturnsFileResult()
        {
            SetAuthUser();
            var content = "test content";
            var fileName = "download.cs";
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(content);
            writer.Flush();
            stream.Position = 0;

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.Length).Returns(stream.Length);
            fileMock.Setup(f => f.ContentType).Returns("text/plain");
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

            var uploadResult = await _controller.UploadFile(fileMock.Object, null);
            var okUpload = Assert.IsType<OkObjectResult>(uploadResult);
            var uploadJson = JObject.FromObject(okUpload.Value);
            var fileId = uploadJson["file"]["Id"].Value<int>();

            var result = await _controller.DownloadFile(fileId);
            Assert.IsAssignableFrom<FileResult>(result);
            var fileResult = result as FileResult;
            Assert.Equal(fileName, fileResult.FileDownloadName);
        }

        [Fact]
        public async Task DownloadFile_FileNotFoundInDb_ReturnsNotFound()
        {
            var result = await _controller.DownloadFile(999);
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var json = JObject.FromObject(notFound.Value);
            Assert.Equal("Файл не найден", json["message"].ToString());
        }

        // -------------------- DeleteFile tests --------------------

        //[Fact]
        //public async Task DeleteFile_AsAuthor_DeletesFile()
        //{
        //    SetAuthUser(1);
        //    var content = "test";
        //    var stream = new MemoryStream();
        //    var writer = new StreamWriter(stream);
        //    writer.Write(content);
        //    writer.Flush();
        //    stream.Position = 0;

        //    var fileMock = new Mock<IFormFile>();
        //    fileMock.Setup(f => f.FileName).Returns("todelete.cs");
        //    fileMock.Setup(f => f.Length).Returns(stream.Length);
        //    fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

        //    var uploadResult = await _controller.UploadFile(fileMock.Object, null);
        //    var okUpload = Assert.IsType<OkObjectResult>(uploadResult);
        //    var uploadJson = JObject.FromObject(okUpload.Value);
        //    var fileId = uploadJson["file"]["Id"].Value<int>();

        //    var result = await _controller.DeleteFile(fileId);
        //    // Используем IsAssignableFrom вместо IsType
        //    var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        //    Assert.Equal(200, objectResult.StatusCode);
        //    var json = JObject.FromObject(objectResult.Value);
        //    Assert.Equal("Файл удалён", json["message"].ToString());

        //    var fileInDb = await _context.UploadedFiles.FindAsync(fileId);
        //    Assert.Null(fileInDb);

        //    var uploadsDir = Path.Combine(_testUploadsPath, "uploads");
        //    if (Directory.Exists(uploadsDir))
        //        Assert.Empty(Directory.GetFiles(uploadsDir));
        //}

        //[Fact]
        //public async Task DeleteFile_AsAdmin_DeletesAnotherUserFile()
        //{
        //    _context.Users.Add(new User { Id = 2, Username = "other", Role = "user", IsActive = true, PasswordHash = "hash" });
        //    await _context.SaveChangesAsync();

        //    SetAuthUser(2);
        //    var content = "test";
        //    var stream = new MemoryStream();
        //    var writer = new StreamWriter(stream);
        //    writer.Write(content);
        //    writer.Flush();
        //    stream.Position = 0;

        //    var fileMock = new Mock<IFormFile>();
        //    fileMock.Setup(f => f.FileName).Returns("admin_delete.cs");
        //    fileMock.Setup(f => f.Length).Returns(stream.Length);
        //    fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

        //    var uploadResult = await _controller.UploadFile(fileMock.Object, null);
        //    var okUpload = Assert.IsType<OkObjectResult>(uploadResult);
        //    var uploadJson = JObject.FromObject(okUpload.Value);
        //    var fileId = uploadJson["file"]["Id"].Value<int>();

        //    SetAdminUser();

        //    var result = await _controller.DeleteFile(fileId);
        //    var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        //    Assert.Equal(200, objectResult.StatusCode);
        //    var json = JObject.FromObject(objectResult.Value);
        //    Assert.Equal("Файл удалён", json["message"].ToString());

        //    var fileInDb = await _context.UploadedFiles.FindAsync(fileId);
        //    Assert.Null(fileInDb);
        //}


        //[Fact]
        //public async Task DeleteFile_AsAnotherUser_ReturnsForbid()
        //{
        //    _context.Users.Add(new User { Id = 2, Username = "other", Role = "user", IsActive = true, PasswordHash = "hash" });
        //    await _context.SaveChangesAsync();

        //    SetAuthUser(1);
        //    var content = "test";
        //    var stream = new MemoryStream();
        //    var writer = new StreamWriter(stream);
        //    writer.Write(content);
        //    writer.Flush();
        //    stream.Position = 0;

        //    var fileMock = new Mock<IFormFile>();
        //    fileMock.Setup(f => f.FileName).Returns("other_delete.cs");
        //    fileMock.Setup(f => f.Length).Returns(stream.Length);
        //    fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

        //    var uploadResult = await _controller.UploadFile(fileMock.Object, null);
        //    var okUpload = Assert.IsType<OkObjectResult>(uploadResult);
        //    var uploadJson = JObject.FromObject(okUpload.Value);
        //    var fileId = uploadJson["file"]["Id"].Value<int>();

        //    SetAuthUser(2);

        //    var result = await _controller.DeleteFile(fileId);
        //    Assert.IsType<ForbidResult>(result);
        //}
    }
}