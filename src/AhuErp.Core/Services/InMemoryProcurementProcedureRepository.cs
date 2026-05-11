using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация <see cref="IProcurementProcedureRepository"/> для тестов.</summary>
    public sealed class InMemoryProcurementProcedureRepository : IProcurementProcedureRepository
    {
        private readonly Dictionary<int, ProcurementProcedure> _store = new Dictionary<int, ProcurementProcedure>();
        private int _next = 1;

        public ProcurementProcedure Add(ProcurementProcedure procedure)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            if (procedure.ProcurementPlanItemId <= 0)
                throw new ArgumentException(
                    "Процедура должна быть привязана к позиции плана.", nameof(procedure));

            procedure.Id = _next++;
            _store[procedure.Id] = procedure;
            return procedure;
        }

        public ProcurementProcedure Update(ProcurementProcedure procedure)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            if (!_store.ContainsKey(procedure.Id))
                throw new InvalidOperationException("Процедура не найдена.");
            _store[procedure.Id] = procedure;
            return procedure;
        }

        public ProcurementProcedure Get(int id)
            => _store.TryGetValue(id, out var p) ? p : null;

        public IReadOnlyList<ProcurementProcedure> ListByItem(int planItemId)
            => _store.Values.Where(p => p.ProcurementPlanItemId == planItemId)
                .OrderBy(p => p.AnnouncedAt ?? DateTime.MaxValue)
                .ToList().AsReadOnly();

        public IReadOnlyList<ProcurementProcedure> List()
            => _store.Values.OrderByDescending(p => p.AnnouncedAt ?? DateTime.MinValue)
                .ToList().AsReadOnly();
    }
}
