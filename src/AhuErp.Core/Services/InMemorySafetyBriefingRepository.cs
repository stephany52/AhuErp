using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="ISafetyBriefingRepository"/> для тестов.
    /// </summary>
    public sealed class InMemorySafetyBriefingRepository : ISafetyBriefingRepository
    {
        private readonly List<SafetyBriefing> _items = new List<SafetyBriefing>();
        private int _nextId = 1;

        public IReadOnlyList<SafetyBriefing> List(DateTime? from, DateTime? to, BriefingKind? kind)
        {
            return _items
                .Where(b => !from.HasValue || b.BriefingDate >= from.Value)
                .Where(b => !to.HasValue || b.BriefingDate <= to.Value)
                .Where(b => !kind.HasValue || b.Kind == kind.Value)
                .OrderByDescending(b => b.BriefingDate)
                .ThenBy(b => b.Id)
                .ToList()
                .AsReadOnly();
        }

        public SafetyBriefing GetById(int id) => _items.FirstOrDefault(b => b.Id == id);

        public void Add(SafetyBriefing briefing)
        {
            if (briefing == null) throw new ArgumentNullException(nameof(briefing));
            if (briefing.Id == 0) briefing.Id = _nextId++;
            else _nextId = Math.Max(_nextId, briefing.Id + 1);
            _items.Add(briefing);
        }

        public void Update(SafetyBriefing briefing)
        {
            if (briefing == null) throw new ArgumentNullException(nameof(briefing));
            var idx = _items.FindIndex(b => b.Id == briefing.Id);
            if (idx < 0) return;
            _items[idx] = briefing;
        }

        public void Remove(int id)
        {
            var existing = _items.FirstOrDefault(b => b.Id == id);
            if (existing != null) _items.Remove(existing);
        }
    }
}
