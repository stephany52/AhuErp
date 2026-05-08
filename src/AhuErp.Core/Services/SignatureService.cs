using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="ISignatureService"/>. Подписи бывают трёх видов
    /// (<see cref="SignatureKind"/>); все подписания и отзывы фиксируются в
    /// журнале аудита с типами <see cref="AuditActionType.SignatureAdded"/>,
    /// <see cref="AuditActionType.SignatureRevoked"/>,
    /// <see cref="AuditActionType.DocumentLocked"/>.
    /// </summary>
    public sealed class SignatureService : ISignatureService
    {
        private readonly ISignatureRepository _signatures;
        private readonly IDocumentRepository _documents;
        private readonly IAttachmentRepository _attachments;
        private readonly IEmployeeRepository _employees;
        private readonly IAuditService _audit;
        private readonly ICryptoProvider _hmac;
        private readonly ICryptoProvider _qualified;

        public SignatureService(
            ISignatureRepository signatures,
            IDocumentRepository documents,
            IAttachmentRepository attachments,
            IEmployeeRepository employees,
            IAuditService audit,
            ICryptoProvider hmac,
            ICryptoProvider qualified = null)
        {
            _signatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            _employees = employees ?? throw new ArgumentNullException(nameof(employees));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _hmac = hmac ?? throw new ArgumentNullException(nameof(hmac));
            _qualified = qualified;
        }

        public DocumentSignature Sign(int documentId, int? attachmentId, int signerId,
                                      SignatureKind kind, string reason = null,
                                      string certificateThumbprint = null)
        {
            var doc = _documents.GetById(documentId)
                ?? throw new InvalidOperationException($"Документ #{documentId} не найден.");
            var signer = _employees.GetById(signerId)
                ?? throw new InvalidOperationException($"Сотрудник #{signerId} не найден.");
            DocumentAttachment att = null;
            if (attachmentId.HasValue)
            {
                att = _attachments.GetById(attachmentId.Value)
                    ?? throw new InvalidOperationException($"Вложение #{attachmentId} не найдено.");
                if (att.DocumentId != documentId)
                    throw new InvalidOperationException("Вложение не принадлежит документу.");
            }

            // Запрет повторной активной подписи одной версии одним сотрудником и того же типа.
            var existingActive = _signatures.ListByDocument(documentId)
                .Where(s => !s.IsRevoked
                            && s.SignerId == signerId
                            && s.AttachmentId == attachmentId
                            && s.Kind == kind);
            if (existingActive.Any())
                throw new InvalidOperationException(
                    "Этот сотрудник уже подписал данную версию подписью того же типа.");

            byte[] payload = BuildPayload(doc, att);
            string hashHex = HexHash(payload);

            byte[] signatureBlob;
            string thumbprint;
            string subject;
            DateTime? notAfter = null;

            if (kind == SignatureKind.Qualified)
            {
                if (_qualified == null)
                    throw new InvalidOperationException("Криптопровайдер для КЭП не зарегистрирован.");
                if (string.IsNullOrEmpty(certificateThumbprint))
                    throw new ArgumentException("Для КЭП обязателен thumbprint сертификата.",
                        nameof(certificateThumbprint));
                thumbprint = certificateThumbprint;
                signatureBlob = _qualified.Sign(payload, thumbprint);
                subject = _qualified.GetSubject(thumbprint);
                // notAfter заполняется реальным провайдером при интеграции.
            }
            else
            {
                thumbprint = !string.IsNullOrEmpty(signer.PasswordHash)
                    ? signer.PasswordHash
                    : $"emp:{signer.Id}";
                signatureBlob = _hmac.Sign(payload, thumbprint);
                subject = $"CN={signer.FullName}; UID={signer.Id}; Kind={kind}";
            }

            var sig = _signatures.Add(new DocumentSignature
            {
                DocumentId = doc.Id,
                AttachmentId = attachmentId,
                SignerId = signerId,
                Kind = kind,
                SignedAt = DateTime.Now,
                SignedHash = hashHex,
                SignatureBlobBase64 = Convert.ToBase64String(signatureBlob),
                CertificateThumbprint = thumbprint,
                CertificateSubject = subject,
                CertificateNotAfter = notAfter,
                Reason = reason,
                IsRevoked = false,
            });

            _audit.Record(AuditActionType.SignatureAdded, nameof(DocumentSignature), sig.Id, signerId,
                newValues: $"DocumentId={doc.Id}; AttachmentId={attachmentId}; Kind={kind}",
                details: reason);

            // Первая Qualified-подпись блокирует документ.
            if (kind == SignatureKind.Qualified && !doc.IsLocked)
            {
                doc.IsLocked = true;
                if (att != null) doc.CurrentVersionAttachmentId = att.Id;
                // Phase 13: Quailfied-подпись продвигает основной DocumentStatus
                // в Signed, если документ был на подписании. TryTransition «тихо»
                // пропускает остальные исходные статусы — это позволяет
                // задним числом подписать черновик без падения.
                DocumentStateMachine.TryTransition(
                    doc, DocumentStatus.Signed,
                    actorRole: null, actorId: signerId, _audit,
                    reason: $"Подписан КЭП (подпись #{sig.Id})");
                _documents.Update(doc);
                _audit.Record(AuditActionType.DocumentLocked, nameof(Document), doc.Id, signerId,
                    newValues: "IsLocked=true", details: "Подписан КЭП");
            }

            return sig;
        }

        public bool Verify(int signatureId)
        {
            var sig = _signatures.Get(signatureId);
            if (sig == null || sig.IsRevoked) return false;

            var doc = _documents.GetById(sig.DocumentId);
            if (doc == null) return false;
            DocumentAttachment att = null;
            if (sig.AttachmentId.HasValue)
            {
                att = _attachments.GetById(sig.AttachmentId.Value);
                if (att == null) return false;
            }

            byte[] payload = BuildPayload(doc, att);
            string hashHex = HexHash(payload);
            if (!string.Equals(hashHex, sig.SignedHash, StringComparison.OrdinalIgnoreCase))
                return false;

            byte[] blob = Convert.FromBase64String(sig.SignatureBlobBase64 ?? string.Empty);
            var provider = sig.Kind == SignatureKind.Qualified ? _qualified : _hmac;
            if (provider == null) return false;
            try
            {
                return provider.Verify(payload, blob, sig.CertificateThumbprint);
            }
            catch (NotSupportedException)
            {
                // Заглушка КЭП.
                return false;
            }
        }

        public void Revoke(int signatureId, int actorId, string reason)
        {
            var sig = _signatures.Get(signatureId)
                ?? throw new InvalidOperationException($"Подпись #{signatureId} не найдена.");
            if (sig.IsRevoked) return;
            sig.IsRevoked = true;
            sig.RevokedAt = DateTime.Now;
            _signatures.Update(sig);

            _audit.Record(AuditActionType.SignatureRevoked, nameof(DocumentSignature), sig.Id, actorId,
                newValues: "IsRevoked=true", details: reason);
        }

        public int RevokeAllNonQualified(int documentId, int actorId, string reason)
        {
            int count = 0;
            foreach (var s in _signatures.ListByDocument(documentId))
            {
                if (!s.IsRevoked && s.Kind != SignatureKind.Qualified)
                {
                    Revoke(s.Id, actorId, reason);
                    count++;
                }
            }
            return count;
        }

        public IReadOnlyList<DocumentSignature> ListByDocument(int documentId)
            => _signatures.ListByDocument(documentId);

        public bool IsDocumentSigned(int documentId, SignatureKind minKind = SignatureKind.Simple)
            => _signatures.ListByDocument(documentId).Any(s => !s.IsRevoked && s.Kind >= minKind);

        // ---------------- helpers ----------------------------------------

        private static byte[] BuildPayload(Document doc, DocumentAttachment att)
        {
            // Подписываем «slice» РКК: ключевые поля + (если есть) метаданные вложения.
            // SizeBytes/Hash вложения включены, поэтому подмена файла → Verify даёт false.
            var sb = new StringBuilder();
            sb.Append(doc.Id).Append('|');
            sb.Append(doc.Title).Append('|');
            sb.Append(doc.RegistrationNumber).Append('|');
            sb.Append(doc.Type).Append('|');
            // AccessLevel намеренно НЕ включён в slice: гриф доступа разрешено
            // менять и после блокировки КЭП (whitelist в DocumentLockGuard /
            // EfDocumentRepository), а Verify не должен из-за этого падать.
            sb.Append(doc.AuthorId).Append('|');
            sb.Append(doc.CreationDate.Ticks).Append('|');
            sb.Append(doc.Deadline.Ticks).Append('|');
            if (att != null)
            {
                sb.Append("ATT|");
                sb.Append(att.Id).Append('|');
                sb.Append(att.FileName).Append('|');
                sb.Append(att.SizeBytes).Append('|');
                sb.Append(att.Hash);
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static string HexHash(byte[] payload)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(payload);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
