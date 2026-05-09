using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 16 / Improvement #17 — настройки учреждения, хранимые в БД
    /// (в отличие от <see cref="OrganizationProfile"/>, который содержит
    /// статические реквизиты). Здесь — только то, что меняется в runtime
    /// и должно быть управляемо администратором: ключ шифрования
    /// конфиденциальных документов, параметры парольной политики, KPI.
    /// Запись всегда одна, с фиксированным <see cref="Id"/> = 1.
    /// </summary>
    public class OrganizationSettings
    {
        /// <summary>Singleton-id записи. В таблице всегда ровно одна строка.</summary>
        public const int SingletonId = 1;

        public int Id { get; set; } = SingletonId;

        /// <summary>
        /// AES-ключ шифрования конфиденциальных полей (Base64 от 32 байт
        /// для AES-256). Может быть <c>null</c> — тогда шифрование
        /// отключено и поле <see cref="Document.Summary"/> хранится в
        /// открытом виде. Регенерация ключа автоматически перешифровывает
        /// существующие записи (см. <c>IDocumentEncryptor.RotateKey</c>).
        /// </summary>
        [StringLength(128)]
        public string EncryptionKey { get; set; }

        /// <summary>
        /// UTC-время генерации текущего <see cref="EncryptionKey"/>.
        /// Используется в админ-панели для отображения «возраста» ключа
        /// и напоминания о ротации.
        /// </summary>
        public DateTime? EncryptionKeyGeneratedAt { get; set; }

        /// <summary>
        /// Минимальная длина пароля. Зафиксировано в политике (8 символов),
        /// но хранится здесь для будущей конфигурации.
        /// </summary>
        public int PasswordMinLength { get; set; } = 8;

        /// <summary>
        /// Срок действия пароля в днях. По умолчанию 90 (Improvement #17).
        /// </summary>
        public int PasswordExpiryDays { get; set; } = 90;

        /// <summary>
        /// Глубина истории паролей. По умолчанию 5 — нельзя повторно
        /// использовать ни один из последних 5 паролей.
        /// </summary>
        public int PasswordHistoryDepth { get; set; } = 5;

        /// <summary>
        /// Сколько неудачных попыток за <see cref="LockoutWindowMinutes"/>
        /// приводят к блокировке. По умолчанию 5.
        /// </summary>
        public int LockoutFailureThreshold { get; set; } = 5;

        /// <summary>
        /// Окно (в минутах) для подсчёта неудачных попыток. По умолчанию 10.
        /// </summary>
        public int LockoutWindowMinutes { get; set; } = 10;

        /// <summary>
        /// Длительность блокировки аккаунта при превышении порога.
        /// По умолчанию 30 минут.
        /// </summary>
        public int LockoutDurationMinutes { get; set; } = 30;
    }
}
