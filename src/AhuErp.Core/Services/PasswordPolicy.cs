using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 16 / Improvement #17 — стандартная реализация
    /// <see cref="IPasswordPolicy"/>. По требованиям заказчика:
    /// <list type="bullet">
    ///   <item><description>минимум 8 символов;</description></item>
    ///   <item><description>хотя бы одна цифра;</description></item>
    ///   <item><description>хотя бы одна заглавная и одна строчная буква
    ///   (любого алфавита, чтобы не отбрасывать кириллицу);</description></item>
    ///   <item><description>срок действия 90 дней;</description></item>
    ///   <item><description>запрет повтора последних 5 паролей.</description></item>
    /// </list>
    /// Конкретные значения порогов читаются из
    /// <see cref="OrganizationSettings"/> — это даёт администратору
    /// возможность ужесточить политику без правки кода.
    /// </summary>
    public sealed class PasswordPolicy : IPasswordPolicy
    {
        private readonly IOrganizationSettingsRepository _settings;

        public PasswordPolicy(IOrganizationSettingsRepository settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public IReadOnlyList<string> ValidateStrength(string password)
        {
            var s = _settings.Get();
            var errors = new List<string>();

            if (string.IsNullOrEmpty(password))
            {
                errors.Add("Пароль не может быть пустым.");
                return errors;
            }

            var minLength = s.PasswordMinLength <= 0 ? 8 : s.PasswordMinLength;
            if (password.Length < minLength)
            {
                errors.Add($"Минимальная длина пароля — {minLength} символов.");
            }

            // Используем char.IsDigit / IsUpper / IsLower (Unicode-aware), чтобы
            // не отбрасывать кириллицу: «А» — заглавная, «а» — строчная, в
            // тестах учреждения это допустимо.
            if (!password.Any(char.IsDigit))
            {
                errors.Add("Пароль должен содержать хотя бы одну цифру.");
            }
            if (!password.Any(char.IsUpper))
            {
                errors.Add("Пароль должен содержать хотя бы одну заглавную букву.");
            }
            if (!password.Any(char.IsLower))
            {
                errors.Add("Пароль должен содержать хотя бы одну строчную букву.");
            }

            return errors;
        }

        public bool ValidateAgainstHistory(string password,
                                           IEnumerable<EmployeePasswordHistory> history,
                                           IPasswordHasher hasher)
        {
            if (string.IsNullOrEmpty(password)) return false;
            if (hasher == null) throw new ArgumentNullException(nameof(hasher));
            if (history == null) return true;

            // Проверяем PBKDF2-хэши последних N (по умолчанию 5) записей.
            // Сортируем по убыванию даты установки на случай, если в БД
            // записи не пришли отсортированными.
            var depth = _settings.Get().PasswordHistoryDepth;
            if (depth <= 0) depth = 5;

            var recent = history
                .OrderByDescending(h => h.SetAt)
                .Take(depth);

            foreach (var entry in recent)
            {
                if (string.IsNullOrEmpty(entry.PasswordHash)) continue;
                if (hasher.Verify(password, entry.PasswordHash))
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsPasswordExpired(DateTime? lastChangedUtc, DateTime nowUtc)
        {
            if (!lastChangedUtc.HasValue) return false;

            var days = _settings.Get().PasswordExpiryDays;
            if (days <= 0) return false;

            return (nowUtc - lastChangedUtc.Value).TotalDays >= days;
        }
    }
}
