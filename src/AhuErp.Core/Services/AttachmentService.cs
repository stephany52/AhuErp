using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IAttachmentService"/>. Гарантирует:
    /// <list type="bullet">
    ///   <item><description>Первая версия вложения формирует группу
    ///     (<see cref="DocumentAttachment.AttachmentGroupId"/> = Id).</description></item>
    ///   <item><description>Загрузка новой версии снимает флаг
    ///     <see cref="DocumentAttachment.IsCurrentVersion"/> у предыдущей.</description></item>
    ///   <item><description>Контролируется хэш SHA-256 содержимого, размер файла
    ///     и запись в журнал аудита.</description></item>
    /// </list>
    /// </summary>
    public sealed class AttachmentService : IAttachmentService
    {
        private readonly IAttachmentRepository _repository;
        private readonly IDocumentRepository _documents;
        private readonly IFileStorageService _storage;
        private readonly IAuditService _audit;
        private readonly ISignatureService _signatures;

        public AttachmentService(
            IAttachmentRepository repository,
            IDocumentRepository documents,
            IFileStorageService storage,
            IAuditService audit,
            ISignatureService signatures = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _signatures = signatures;
        }

        public DocumentAttachment Upload(int documentId, Stream content, string fileName,
            int uploadedById, AttachmentKind kind = AttachmentKind.Draft, string comment = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (uploadedById <= 0) throw new ArgumentException("Загрузивший пользователь обязателен.");

            var doc = _documents.GetById(documentId)
                ?? throw new InvalidOperationException($"Документ #{documentId} не найден.");

            var (path, hash, size) = SaveAndHash(content, doc.RegistrationNumber, version: 1, fileName);

            var attachment = new DocumentAttachment
            {
                DocumentId = doc.Id,
                FileName = fileName,
                StoragePath = path,
                VersionNumber = 1,
                IsCurrentVersion = true,
                UploadedAt = DateTime.UtcNow,
                UploadedById = uploadedById,
                Comment = comment,
                Hash = hash,
                FileType = kind,
                SizeBytes = size
            };
            attachment = _repository.Add(attachment);
            // Группа = идентификатор первой версии. Для дальнейших версий
            // используем тот же AttachmentGroupId, что фиксирует «логическое»
            // вложение и упрощает поиск истории.
            attachment.AttachmentGroupId = attachment.Id;
            _repository.Update(attachment);

            _audit.Record(AuditActionType.AttachmentAdded, nameof(DocumentAttachment), attachment.Id,
                uploadedById, newValues: $"DocumentId={doc.Id}; FileName={fileName}; Version=1");

            return attachment;
        }

        public DocumentAttachment AddVersion(int attachmentGroupId, Stream content, string fileName,
            int uploadedById, string comment = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (uploadedById <= 0) throw new ArgumentException("Загрузивший пользователь обязателен.");

            var current = _repository.GetCurrentByGroup(attachmentGroupId)
                ?? throw new InvalidOperationException(
                    $"Логическое вложение #{attachmentGroupId} не найдено.");

            var doc = _documents.GetById(current.DocumentId);
            var nextVersion = _repository.GetMaxVersionInGroup(attachmentGroupId) + 1;

            var (path, hash, size) = SaveAndHash(content, doc?.RegistrationNumber, nextVersion, fileName);

            current.IsCurrentVersion = false;
            _repository.Update(current);

            var version = new DocumentAttachment
            {
                DocumentId = current.DocumentId,
                AttachmentGroupId = attachmentGroupId,
                FileName = fileName,
                StoragePath = path,
                VersionNumber = nextVersion,
                IsCurrentVersion = true,
                UploadedAt = DateTime.UtcNow,
                UploadedById = uploadedById,
                Comment = comment,
                Hash = hash,
                FileType = current.FileType,
                SizeBytes = size
            };
            version = _repository.Add(version);

            _audit.Record(AuditActionType.AttachmentVersioned, nameof(DocumentAttachment), version.Id,
                uploadedById, newValues: $"GroupId={attachmentGroupId}; Version={nextVersion}");

            // Phase 8 — загрузка новой версии автоматически отзывает ПЭП/НЭП
            // (квалифицированные подписи КЭП требуют ручного отзыва оператором).
            if (_signatures != null && doc != null)
            {
                _signatures.RevokeAllNonQualified(doc.Id, uploadedById,
                    reason: $"Загружена новая версия #{nextVersion} вложения #{attachmentGroupId}");
            }

            return version;
        }

        public IReadOnlyList<DocumentAttachment> ListByDocument(int documentId)
            => _repository.ListByDocument(documentId);

        public IReadOnlyList<DocumentAttachment> ListVersions(int attachmentGroupId)
            => _repository.ListByGroup(attachmentGroupId);

        public Stream Open(int attachmentId, int viewedById)
        {
            var attachment = _repository.GetById(attachmentId)
                ?? throw new InvalidOperationException($"Вложение #{attachmentId} не найдено.");
            _audit.Record(AuditActionType.AttachmentViewed, nameof(DocumentAttachment), attachment.Id,
                viewedById, details: attachment.FileName);
            return _storage.Open(attachment.StoragePath);
        }

        public Stream Download(int attachmentId, int downloadedById)
        {
            var attachment = _repository.GetById(attachmentId)
                ?? throw new InvalidOperationException($"Вложение #{attachmentId} не найдено.");

            // Phase 16 / Improvement #17 — отдельное событие для журнала аудита
            // (отличается от AttachmentViewed: «скачал на свой диск», а не «открыл
            // в карточке»). EntityId записываем как DocumentId, чтобы выборка
            // «история документа» в админ-панели включала факт скачивания.
            _audit.Record(AuditActionType.DocumentDownloaded, nameof(Document),
                attachment.DocumentId, downloadedById,
                details: $"attachmentId={attachment.Id}; file={attachment.FileName}");
            return _storage.Open(attachment.StoragePath);
        }

        private (string path, string hash, long size) SaveAndHash(Stream content, string registrationNumber, int version, string fileName)
        {
            // Хэшируем поток в памяти, затем перематываем для записи в хранилище;
            // если поток не позволяет seek — копируем в MemoryStream.
            byte[] buffer;
            using (var ms = new MemoryStream())
            {
                content.CopyTo(ms);
                buffer = ms.ToArray();
            }
            var hash = ComputeSha256(buffer);
            string path;
            using (var ms = new MemoryStream(buffer, writable: false))
            {
                path = _storage.Store(ms, registrationNumber, version, fileName);
            }
            return (path, hash, buffer.LongLength);
        }

        private static string ComputeSha256(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(data);
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
