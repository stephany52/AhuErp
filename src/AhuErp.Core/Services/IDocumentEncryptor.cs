namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 16 / Improvement #17 — шифрование чувствительных полей
    /// (в первую очередь <see cref="Models.Document.Summary"/> при
    /// <see cref="Models.DocumentAccessLevel.Confidential"/>). Реализация —
    /// AES-256-CBC + HMAC-SHA256 (encrypt-then-MAC) на ключе из
    /// <see cref="Models.OrganizationSettings.EncryptionKey"/>.
    /// </summary>
    public interface IDocumentEncryptor
    {
        /// <summary>
        /// Включено ли шифрование (есть ли в настройках валидный ключ).
        /// Если false, <see cref="Encrypt"/> возвращает исходную строку.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Шифрует строку. Если шифрование выключено или строка пуста —
        /// возвращает её без изменений. Возвращаемая строка имеет префикс
        /// <c>"enc:v1:"</c>, что делает обнаружение шифротекста однозначным.
        /// </summary>
        string Encrypt(string plaintext);

        /// <summary>
        /// Расшифровывает строку. Если у строки нет префикса <c>"enc:v1:"</c>,
        /// она считается plaintext-ом и возвращается как есть (для совместимости
        /// с старыми записями до миграции и c записями неконфиденциальных
        /// документов). При повреждённом MAC бросает
        /// <see cref="System.Security.Cryptography.CryptographicException"/>.
        /// </summary>
        string Decrypt(string ciphertext);

        /// <summary>
        /// Считает строку зашифрованным контейнером по префиксу
        /// <c>"enc:v1:"</c>. Утилитарный метод для миграции/диагностики.
        /// </summary>
        bool IsEncryptedPayload(string value);

        /// <summary>
        /// Генерирует новый случайный AES-ключ (32 байта, base64) и сохраняет
        /// его в <see cref="Models.OrganizationSettings"/> вместе с UTC-меткой
        /// генерации. Возвращает сгенерированный ключ (для записи в аудит и
        /// для отображения в UI «ключ обновлён»).
        /// </summary>
        string RotateKey();
    }
}
