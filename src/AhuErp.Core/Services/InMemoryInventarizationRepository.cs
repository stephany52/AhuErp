using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IInventarizationRepository"/> для тестов.
    /// </summary>
    public sealed class InMemoryInventarizationRepository : IInventarizationRepository
    {
        private readonly List<Inventarization> _items = new List<Inventarization>();
        private int _nextId = 1;
        private int _nextDiscrepancyId = 1;

        public IReadOnlyList<Inventarization> List(DateTime? from, DateTime? to, InventarizationScope? scope)
        {
            return _items
                .Where(i => !from.HasValue || i.StartDate >= from.Value)
                .Where(i => !to.HasValue || i.StartDate <= to.Value)
                .Where(i => !scope.HasValue || i.Scope == scope.Value)
                .OrderByDescending(i => i.StartDate)
                .ThenBy(i => i.Id)
                .ToList()
                .AsReadOnly();
        }

        public Inventarization GetById(int id) => _items.FirstOrDefault(i => i.Id == id);

        public void Add(Inventarization inventarization)
        {
            if (inventarization == null) throw new ArgumentNullException(nameof(inventarization));
            if (inventarization.Id == 0) inventarization.Id = _nextId++;
            else _nextId = Math.Max(_nextId, inventarization.Id + 1);
            foreach (var d in inventarization.Discrepancies)
            {
                if (d.Id == 0) d.Id = _nextDiscrepancyId++;
                else _nextDiscrepancyId = Math.Max(_nextDiscrepancyId, d.Id + 1);
                d.InventarizationId = inventarization.Id;
            }
            _items.Add(inventarization);
        }

        public void Update(Inventarization inventarization)
        {
            if (inventarization == null) throw new ArgumentNullException(nameof(inventarization));
            var idx = _items.FindIndex(i => i.Id == inventarization.Id);
            if (idx < 0) return;
            foreach (var d in inventarization.Discrepancies)
            {
                if (d.Id == 0) d.Id = _nextDiscrepancyId++;
                else _nextDiscrepancyId = Math.Max(_nextDiscrepancyId, d.Id + 1);
                d.InventarizationId = inventarization.Id;
            }
            _items[idx] = inventarization;
        }

        public void Remove(int id)
        {
            var existing = _items.FirstOrDefault(i => i.Id == id);
            if (existing != null) _items.Remove(existing);
        }
    }
}
