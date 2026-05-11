using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="IProcurementProcedureRepository"/>.</summary>
    public sealed class EfProcurementProcedureRepository : IProcurementProcedureRepository
    {
        private readonly AhuDbContext _ctx;

        public EfProcurementProcedureRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public ProcurementProcedure Add(ProcurementProcedure procedure)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            if (procedure.ProcurementPlanItemId <= 0)
                throw new ArgumentException(
                    "Процедура должна быть привязана к позиции плана.", nameof(procedure));
            _ctx.ProcurementProcedures.Add(procedure);
            _ctx.SaveChanges();
            return procedure;
        }

        public ProcurementProcedure Update(ProcurementProcedure procedure)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            _ctx.Entry(procedure).State = EntityState.Modified;
            _ctx.SaveChanges();
            return procedure;
        }

        public ProcurementProcedure Get(int id) => _ctx.ProcurementProcedures.Find(id);

        public IReadOnlyList<ProcurementProcedure> ListByItem(int planItemId)
            => _ctx.ProcurementProcedures
                .Where(p => p.ProcurementPlanItemId == planItemId)
                .OrderBy(p => p.AnnouncedAt)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<ProcurementProcedure> List()
            => _ctx.ProcurementProcedures
                .OrderByDescending(p => p.AnnouncedAt)
                .ToList()
                .AsReadOnly();
    }
}
