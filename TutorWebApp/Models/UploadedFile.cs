using System.ComponentModel.DataAnnotations;

namespace TutorApi.Models
{
    public class UploadedFile
    {
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string ContentType { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // Связь с уроком (опционально)
        public int? LessonId { get; set; }
        public Lesson? Lesson { get; set; }

        // Кто загрузил
        public int UserId { get; set; }
        public User? User { get; set; }
    }
}
