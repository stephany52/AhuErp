using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Хранилище журнала передачи дел в архив. Improvement #12 / Phase 15.
    /// </summary>
    public interface IArchiveTransferRepository
    {
        IReadOnlyList<ArchiveTransfer> List(DateTime? from, DateTime? to);
        IReadOnlyList<ArchiveTransfer> ListByCase(int nomenclatureCaseId);
        ArchiveTransfer GetById(int id);
        void Add(ArchiveTransfer transfer);
        void Update(ArchiveTransfer transfer);
        void Remove(int id);
    }
}
