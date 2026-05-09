using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IArchiveTransferRepository"/> для тестов.
    /// </summary>
    public sealed class InMemoryArchiveTransferRepository : IArchiveTransferRepository
    {
        private readonly List<ArchiveTransfer> _items = new List<ArchiveTransfer>();
        private int _nextId = 1;

        public IReadOnlyList<ArchiveTransfer> List(DateTime? from, DateTime? to)
        {
            return _items
                .Where(t => !from.HasValue || t.TransferDate >= from.Value)
                .Where(t => !to.HasValue || t.TransferDate <= to.Value)
                .OrderByDescending(t => t.TransferDate)
                .ThenBy(t => t.Id)
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<ArchiveTransfer> ListByCase(int nomenclatureCaseId)
        {
            return _items
                .Where(t => t.NomenclatureCaseId == nomenclatureCaseId)
                .OrderByDescending(t => t.TransferDate)
                .ThenBy(t => t.Id)
                .ToList()
                .AsReadOnly();
        }

        public ArchiveTransfer GetById(int id) => _items.FirstOrDefault(t => t.Id == id);

        public void Add(ArchiveTransfer transfer)
        {
            if (transfer == null) throw new ArgumentNullException(nameof(transfer));
            if (transfer.Id == 0) transfer.Id = _nextId++;
            else _nextId = Math.Max(_nextId, transfer.Id + 1);
            _items.Add(transfer);
        }

        public void Update(ArchiveTransfer transfer)
        {
            if (transfer == null) throw new ArgumentNullException(nameof(transfer));
            var idx = _items.FindIndex(t => t.Id == transfer.Id);
            if (idx < 0) return;
            _items[idx] = transfer;
        }

        public void Remove(int id)
        {
            var existing = _items.FirstOrDefault(t => t.Id == id);
            if (existing != null) _items.Remove(existing);
        }
    }
}
