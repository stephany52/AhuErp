using System;
using System.ComponentModel.DataAnnotations;
using AhuErp.Core.Services;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 16 / Improvement #17 — журнал попыток входа в систему. Запись
    /// создаётся как для успешных, так и для неудачных попыток. Используется:
    /// 1) для блокировки аккаунта (5 неудачных попыток за 10 минут → блок 30 мин);
    /// 2) для аудита подозрительной активности в админ-панели;
    /// 3) для расследования инцидентов безопасности.
    /// </summary>
    public class LoginAttempt
    {
        public int Id { get; set; }

        /// <summary>
        /// Идентификатор сотрудника, если ФИО при входе сопоставилось с
        /// существующей учёткой. Для попыток с неизвестным ФИО хранится
        /// <c>null</c>; такие попытки попадают в журнал по
        /// <see cref="AttemptedFullName"/>.
        /// </summary>
        public int? EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        /// <summary>
        /// ФИО, под которым была попытка входа. Сохраняется как введено
        /// пользователем (после нормализации NFC), чтобы видеть сценарии
        /// перебора с опечатками или подменой.
        /// </summary>
        [StringLength(256)]
        public string AttemptedFullName { get; set; }

        /// <summary>UTC-время попытки.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// IP-адрес клиента (или иная сетевая координата). На локальной
        /// инсталляции допускается значение <c>"local"</c>.
        /// </summary>
        [StringLength(64)]
        public string IpAddress { get; set; }

        public bool Success { get; set; }

        /// <summary>
        /// Причина неуспеха для аналитики. Для успешных попыток —
        /// <see cref="LoginFailureReason.None"/>.
        /// </summary>
        public LoginFailureReason FailureReason { get; set; }
    }
}
