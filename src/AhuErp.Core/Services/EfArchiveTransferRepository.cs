using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IArchiveTransferRepository"/> поверх <see cref="AhuDbContext"/>.
    /// </summary>
    public sealed class EfArchiveTransferRepository : IArchiveTransferRepository
    {
        private readonly AhuDbContext _ctx;

        public EfArchiveTransferRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public IReadOnlyList<ArchiveTransfer> List(DateTime? from, DateTime? to)
        {
            IQueryable<ArchiveTransfer> q = _ctx.ArchiveTransfers
                .Include(t => t.NomenclatureCase)
                .Include(t => t.TransferredBy)
                .Include(t => t.AcceptedBy)
                .Include(t => t.ActDocument);
            if (from.HasValue) q = q.Where(t => t.TransferDate >= from.Value);
            if (to.HasValue) q = q.Where(t => t.TransferDate <= to.Value);
            return q.OrderByDescending(t => t.TransferDate)
                    .ThenBy(t => t.Id)
                    .ToList()
                    .AsReadOnly();
        }

        public IReadOnlyList<ArchiveTransfer> ListByCase(int nomenclatureCaseId) =>
            _ctx.ArchiveTransfers
                .Include(t => t.NomenclatureCase)
                .Where(t => t.NomenclatureCaseId == nomenclatureCaseId)
                .OrderByDescending(t => t.TransferDate)
                .ThenBy(t => t.Id)
                .ToList()
                .AsReadOnly();

        public ArchiveTransfer GetById(int id) =>
            _ctx.ArchiveTransfers
                .Include(t => t.NomenclatureCase)
                .Include(t => t.TransferredBy)
                .Include(t => t.AcceptedBy)
                .Include(t => t.ActDocument)
                .FirstOrDefault(t => t.Id == id);

        public void Add(ArchiveTransfer transfer)
        {
            if (transfer == null) throw new ArgumentNullException(nameof(transfer));
            _ctx.ArchiveTransfers.Add(transfer);
            _ctx.SaveChanges();
        }

        public void Update(ArchiveTransfer transfer)
        {
            if (transfer == null) throw new ArgumentNullException(nameof(transfer));
            _ctx.Entry(transfer).State = EntityState.Modified;
            _ctx.SaveChanges();
        }

        public void Remove(int id)
        {
            var existing = _ctx.ArchiveTransfers.Find(id);
            if (existing == null) return;
            _ctx.ArchiveTransfers.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
