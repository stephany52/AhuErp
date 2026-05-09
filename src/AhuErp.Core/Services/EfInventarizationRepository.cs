using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IInventarizationRepository"/> поверх <see cref="AhuDbContext"/>.
    /// </summary>
    public sealed class EfInventarizationRepository : IInventarizationRepository
    {
        private readonly AhuDbContext _ctx;

        public EfInventarizationRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public IReadOnlyList<Inventarization> List(DateTime? from, DateTime? to, InventarizationScope? scope)
        {
            IQueryable<Inventarization> q = _ctx.Inventarizations
                .Include(i => i.Chairman)
                .Include(i => i.ResultDocument)
                .Include(i => i.Discrepancies);
            if (from.HasValue) q = q.Where(i => i.StartDate >= from.Value);
            if (to.HasValue) q = q.Where(i => i.StartDate <= to.Value);
            if (scope.HasValue) q = q.Where(i => i.Scope == scope.Value);
            return q.OrderByDescending(i => i.StartDate)
                    .ThenBy(i => i.Id)
                    .ToList()
                    .AsReadOnly();
        }

        public Inventarization GetById(int id) =>
            _ctx.Inventarizations
                .Include(i => i.Chairman)
                .Include(i => i.ResultDocument)
                .Include(i => i.Discrepancies)
                .FirstOrDefault(i => i.Id == id);

        public void Add(Inventarization inventarization)
        {
            if (inventarization == null) throw new ArgumentNullException(nameof(inventarization));
            _ctx.Inventarizations.Add(inventarization);
            _ctx.SaveChanges();
        }

        public void Update(Inventarization inventarization)
        {
            if (inventarization == null) throw new ArgumentNullException(nameof(inventarization));
            _ctx.Entry(inventarization).State = EntityState.Modified;
            _ctx.SaveChanges();
        }

        public void Remove(int id)
        {
            var existing = _ctx.Inventarizations
                .Include(i => i.Discrepancies)
                .FirstOrDefault(i => i.Id == id);
            if (existing == null) return;
            _ctx.Inventarizations.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
