using System.Collections.Generic;
using System.IO;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Управление вложениями документа с поддержкой версионирования.
    /// </summary>
    public interface IAttachmentService
    {
        /// <summary>
        /// Загрузить новое (первая версия) вложение к документу.
        /// </summary>
        DocumentAttachment Upload(int documentId, Stream content, string fileName,
            int uploadedById, AttachmentKind kind = AttachmentKind.Draft, string comment = null);

        /// <summary>
        /// Загрузить новую версию существующего «логического» вложения.
        /// Старая версия снимает флаг <see cref="DocumentAttachment.IsCurrentVersion"/>.
        /// </summary>
        DocumentAttachment AddVersion(int attachmentGroupId, Stream content, string fileName,
            int uploadedById, string comment = null);

        IReadOnlyList<DocumentAttachment> ListByDocument(int documentId);

        /// <summary>Все версии одного логического вложения, упорядоченные по версии.</summary>
        IReadOnlyList<DocumentAttachment> ListVersions(int attachmentGroupId);

        Stream Open(int attachmentId, int viewedById);

        /// <summary>
        /// Phase 16 / Improvement #17 — выгрузка вложения на диск пользователя.
        /// В отличие от <see cref="Open"/>, эмитит отдельное событие
        /// <see cref="AuditActionType.DocumentDownloaded"/> для журнала аудита.
        /// </summary>
        Stream Download(int attachmentId, int downloadedById);
    }
}
