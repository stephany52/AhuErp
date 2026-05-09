using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Сотрудник учреждения. Может быть назначен ответственным за документ
    /// и аутентифицироваться в системе (см. <see cref="PasswordHash"/>).
    /// </summary>
    public class Employee
    {
        public int Id { get; set; }

        [Required]
        [StringLength(256)]
        public string FullName { get; set; }

        [StringLength(256)]
        public string Position { get; set; }

        /// <summary>
        /// Роль сотрудника в системе. По умолчанию — ограниченный
        /// <see cref="EmployeeRole.TechSupport"/>, до явного повышения.
        /// </summary>
        public EmployeeRole Role { get; set; } = EmployeeRole.TechSupport;

        /// <summary>
        /// Хэш пароля в формате <c>{iterations}.{base64(salt)}.{base64(hash)}</c>,
        /// рассчитанный через <see cref="Services.IPasswordHasher"/>.
        /// Ни в каком виде чистый пароль не сохраняется.
        /// </summary>
        [StringLength(512)]
        public string PasswordHash { get; set; }

        /// <summary>
        /// E-mail сотрудника (Phase 9). Используется <c>SmtpEmailGateway</c>
        /// при отправке нотификаций; null/пусто → канал e-mail отключён.
        /// </summary>
        [StringLength(256)]
        public string Email { get; set; }

        /// <summary>Подразделение сотрудника (Phase 11).</summary>
        public int? DepartmentId { get; set; }
        public virtual Department Department { get; set; }

        /// <summary>Активен ли сотрудник (Phase 11). false = уволен/деактивирован.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Дата прекращения трудовых отношений (Phase 11).</summary>
        public DateTime? TerminatedAt { get; set; }

        /// <summary>
        /// Phase 16 / Improvement #17 — UTC-дата последней смены пароля.
        /// Используется для проверки 90-дневного срока действия пароля.
        /// <c>null</c> у демо-учёток / до первой смены — система считает,
        /// что срок не истёк (для совместимости со seeded-данными).
        /// </summary>
        public DateTime? LastPasswordChangeAt { get; set; }

        /// <summary>
        /// Phase 16 / Improvement #17 — UTC-время до которого учётка
        /// заблокирована (lockout). Если значение установлено и больше
        /// <c>DateTime.UtcNow</c>, любая попытка входа отклоняется
        /// с <see cref="Services.LoginFailureReason.AccountLocked"/>.
        /// Сбрасывается администратором или автоматически по истечении.
        /// </summary>
        public DateTime? LockedUntil { get; set; }

        public virtual ICollection<Document> AssignedDocuments { get; set; } = new HashSet<Document>();

        /// <summary>
        /// Phase 16 — история паролей сотрудника (последние N штук).
        /// Используется для проверки запрета повторного использования.
        /// </summary>
        public virtual ICollection<EmployeePasswordHistory> PasswordHistory { get; set; }
            = new HashSet<EmployeePasswordHistory>();
    }
}
