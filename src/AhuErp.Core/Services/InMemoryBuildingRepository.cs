using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация <see cref="IBuildingRepository"/> для тестов.</summary>
    public sealed class InMemoryBuildingRepository : IBuildingRepository
    {
        private readonly Dictionary<int, Building> _store = new Dictionary<int, Building>();
        private int _next = 1;

        public Building Add(Building building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (string.IsNullOrWhiteSpace(building.Name))
                throw new ArgumentException("Наименование здания обязательно.", nameof(building));
            if (_store.Values.Any(b => b.Name == building.Name))
                throw new InvalidOperationException(
                    $"Здание с наименованием «{building.Name}» уже зарегистрировано.");

            building.Id = _next++;
            _store[building.Id] = building;
            return building;
        }

        public Building Get(int id) => _store.TryGetValue(id, out var b) ? b : null;

        public Building GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return _store.Values.FirstOrDefault(b => b.Name == name);
        }

        public IReadOnlyList<Building> List()
            => _store.Values.OrderBy(b => b.Name).ToList().AsReadOnly();

        public Building Update(Building building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (!_store.ContainsKey(building.Id))
                throw new InvalidOperationException("Здание не найдено.");
            _store[building.Id] = building;
            return building;
        }

        public void Delete(int id) => _store.Remove(id);
    }
}
