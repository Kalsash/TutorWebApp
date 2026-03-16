using System.ComponentModel.DataAnnotations;

namespace TutorApi.Models
{
    public class Lesson
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty; // HTML от CKEditor

        [Url]
        public string? MeetingLink { get; set; } // ссылка на Яндекс Телемост

        [Url]
        public string? BoardLink { get; set; } // ссылка на Яндекс Телемост

        public bool IsPublished { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Кто создал (админ)
        public int AuthorId { get; set; }
        public User? Author { get; set; }
    }
}
