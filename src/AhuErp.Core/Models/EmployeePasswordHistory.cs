using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 16 / Improvement #17 — история паролей сотрудника. Хранит
    /// последние N (по умолчанию 5) хэшей паролей, чтобы запретить повтор
    /// при смене пароля. Запись добавляется при каждой успешной смене
    /// и не редактируется. Удаление допустимо только при ротации
    /// (превышении лимита истории).
    /// </summary>
    public class EmployeePasswordHistory
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        /// <summary>
        /// Хэш в том же формате, что и <see cref="Employee.PasswordHash"/>
        /// (см. <see cref="Services.Pbkdf2PasswordHasher"/>).
        /// </summary>
        [Required]
        [StringLength(512)]
        public string PasswordHash { get; set; }

        /// <summary>UTC-время установки пароля.</summary>
        public DateTime SetAt { get; set; }
    }
}
