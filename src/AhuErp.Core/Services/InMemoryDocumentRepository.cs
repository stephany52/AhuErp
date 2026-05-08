using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация документа, сохраняющая порядок добавления и
    /// присваивающая инкрементальный Id. Служит и для тестов, и для демо-режима.
    /// </summary>
    public sealed class InMemoryDocumentRepository : IDocumentRepository
    {
        private readonly List<Document> _documents = new List<Document>();
        private readonly Dictionary<int, DocumentLockSnapshot> _snapshots
            = new Dictionary<int, DocumentLockSnapshot>();
        private int _nextId = 1;

        public IReadOnlyList<Document> ListByType(DocumentType type)
        {
            return _documents.Where(d => d.Type == type
                                         && !(d is ArchiveRequest)
                                         && !(d is ItTicket))
                             .ToList()
                             .AsReadOnly();
        }

        public IReadOnlyList<ArchiveRequest> ListArchiveRequests()
        {
            return _documents.OfType<ArchiveRequest>().ToList().AsReadOnly();
        }

        public IReadOnlyList<ItTicket> ListItTickets()
        {
            return _documents.OfType<ItTicket>().ToList().AsReadOnly();
        }

        public IReadOnlyList<Document> ListInventoryEligibleDocuments()
        {
            return _documents.Where(d => d.Type == DocumentType.Internal
                                         || d.Type == DocumentType.It
                                         || d is ItTicket)
                             .ToList()
                             .AsReadOnly();
        }

        public Document GetById(int id) => _documents.FirstOrDefault(d => d.Id == id);

        public void Add(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            Document.ValidateRegistrationNumber(document.RegistrationNumber);
            if (document.Id == 0) document.Id = _nextId++;
            else _nextId = Math.Max(_nextId, document.Id + 1);
            _documents.Add(document);
            _snapshots[document.Id] = DocumentLockSnapshot.Of(document);
        }

        public void Update(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            Document.ValidateRegistrationNumber(document.RegistrationNumber);
            var index = _documents.FindIndex(d => d.Id == document.Id);
            if (index < 0)
                throw new InvalidOperationException($"Документ #{document.Id} не найден.");
            // Phase 8 — если документ был заблокирован подписью, разрешено менять
            // только Status / AssignedEmployeeId / AccessLevel / IsLocked /
            // CurrentVersionAttachmentId. Прочие правки → исключение.
            if (_snapshots.TryGetValue(document.Id, out var snap))
                snap.Enforce(document);
            _documents[index] = document;
            _snapshots[document.Id] = DocumentLockSnapshot.Of(document);
        }

        public void Remove(int id)
        {
            var index = _documents.FindIndex(d => d.Id == id);
            if (index >= 0)
            {
                _documents.RemoveAt(index);
                _snapshots.Remove(id);
            }
        }

        public IReadOnlyList<Document> Search(DocumentSearchFilter filter)
        {
            if (filter == null) filter = new DocumentSearchFilter();
            IEnumerable<Document> q = _documents;

            if (filter.Direction.HasValue)
                q = q.Where(d => d.Direction == filter.Direction.Value);

            if (filter.StatusIn != null && filter.StatusIn.Length > 0)
                q = q.Where(d => filter.StatusIn.Contains(d.Status));
            else if (filter.Status.HasValue)
                q = q.Where(d => d.Status == filter.Status.Value);

            if (filter.NomenclatureCaseId.HasValue)
                q = q.Where(d => d.NomenclatureCaseId == filter.NomenclatureCaseId.Value);

            if (filter.DocumentTypeRefId.HasValue)
                q = q.Where(d => d.DocumentTypeRefId == filter.DocumentTypeRefId.Value);

            if (filter.AssignedEmployeeId.HasValue)
                q = q.Where(d => d.AssignedEmployeeId == filter.AssignedEmployeeId.Value);

            if (filter.RegisteredOnly)
                q = q.Where(d => !string.IsNullOrEmpty(d.RegistrationNumber) && d.RegistrationDate.HasValue);

            if (filter.From.HasValue)
                q = q.Where(d => (d.RegistrationDate ?? d.CreationDate) >= filter.From.Value);

            if (filter.To.HasValue)
                q = q.Where(d => (d.RegistrationDate ?? d.CreationDate) <= filter.To.Value);

            if (!string.IsNullOrWhiteSpace(filter.Correspondent))
            {
                var c = filter.Correspondent.Trim();
                q = q.Where(d => !string.IsNullOrEmpty(d.Correspondent)
                                 && d.Correspondent.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (!string.IsNullOrWhiteSpace(filter.Text))
            {
                var t = filter.Text.Trim();
                q = q.Where(d =>
                       Contains(d.Title, t)
                    || Contains(d.Summary, t)
                    || Contains(d.RegistrationNumber, t)
                    || Contains(d.Correspondent, t)
                    || Contains(d.IncomingNumber, t));
            }

            if (filter.OverdueOnly)
            {
                var now = DateTime.Now;
                q = q.Where(d => d.IsOverdue(now));
            }

            return q.OrderByDescending(d => d.RegistrationDate ?? d.CreationDate)
                    .ThenByDescending(d => d.Id)
                    .ToList()
                    .AsReadOnly();
        }

        private static bool Contains(string source, string token)
            => !string.IsNullOrEmpty(source)
               && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
