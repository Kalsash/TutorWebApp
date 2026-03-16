using System.ComponentModel.DataAnnotations;

namespace TutorApi.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty; // Логин (для входа)

        [MaxLength(100)]
        public string? FirstName { get; set; } = string.Empty; // Имя

        [MaxLength(100)]
        public string? LastName { get; set; } = string.Empty; // Фамилия

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Role { get; set; } = "user";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        // Свойство для отображения полного имени
        public string FullName => $"{LastName} {FirstName}".Trim();
    }
}
