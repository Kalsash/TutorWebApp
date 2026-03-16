using System.Text.RegularExpressions;

namespace TutorWebApp.Services
{
    public interface IValidationService
    {
        ValidationResult ValidateUsername(string username);
        ValidationResult ValidatePassword(string password);
        ValidationResult ValidateNewPassword(string password, bool isReset = false);
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public static ValidationResult Success() => new() { IsValid = true };
        public static ValidationResult Fail(string message) => new() { IsValid = false, ErrorMessage = message };
    }

    public class ValidationService : IValidationService
    {
        private const int MinUsernameLength = 3;
        private const int MaxUsernameLength = 50;
        private const int MinPasswordLength = 6;
        private const int MinResetPasswordLength = 3;

        public ValidationResult ValidateUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return ValidationResult.Fail("Имя пользователя не может быть пустым");

            if (username.Length < MinUsernameLength)
                return ValidationResult.Fail($"Имя пользователя должно быть не менее {MinUsernameLength} символов");

            if (username.Length > MaxUsernameLength)
                return ValidationResult.Fail($"Имя пользователя должно быть не более {MaxUsernameLength} символов");

            // Разрешаем буквы, цифры, точки и подчеркивания
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9._]+$"))
                return ValidationResult.Fail("Имя пользователя может содержать только буквы, цифры, точки и подчеркивания");

            return ValidationResult.Success();
        }

        public ValidationResult ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return ValidationResult.Fail("Пароль не может быть пустым");

            if (password.Length < MinPasswordLength)
                return ValidationResult.Fail($"Пароль должен быть не менее {MinPasswordLength} символов");

            // Проверка на сложность пароля
            var hasUpperCase = password.Any(char.IsUpper);
            var hasLowerCase = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);

            if (!hasUpperCase || !hasLowerCase || !hasDigit)
                return ValidationResult.Fail("Пароль должен содержать хотя бы одну заглавную букву, одну строчную букву и одну цифру");

            return ValidationResult.Success();
        }

        public ValidationResult ValidateNewPassword(string password, bool isReset = false)
        {
            if (string.IsNullOrWhiteSpace(password))
                return ValidationResult.Fail("Пароль не может быть пустым");

            var minLength = isReset ? MinResetPasswordLength : MinPasswordLength;

            if (password.Length < minLength)
                return ValidationResult.Fail($"Пароль должен быть не менее {minLength} символов");

            // При сбросе пароля админом не требуем сложности
            if (!isReset)
            {
                var hasUpperCase = password.Any(char.IsUpper);
                var hasLowerCase = password.Any(char.IsLower);
                var hasDigit = password.Any(char.IsDigit);

                if (!hasUpperCase || !hasLowerCase || !hasDigit)
                    return ValidationResult.Fail("Пароль должен содержать хотя бы одну заглавную букву, одну строчную букву и одну цифру");
            }

            return ValidationResult.Success();
        }
    }
}
