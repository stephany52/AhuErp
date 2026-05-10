using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="IBuildingRepository"/>.</summary>
    public sealed class EfBuildingRepository : IBuildingRepository
    {
        private readonly AhuDbContext _ctx;

        public EfBuildingRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public Building Add(Building building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (string.IsNullOrWhiteSpace(building.Name))
                throw new ArgumentException("Наименование здания обязательно.", nameof(building));

            if (_ctx.Buildings.Any(b => b.Name == building.Name))
                throw new InvalidOperationException(
                    $"Здание с наименованием «{building.Name}» уже зарегистрировано.");

            _ctx.Buildings.Add(building);
            _ctx.SaveChanges();
            return building;
        }

        public Building Get(int id) => _ctx.Buildings.Find(id);

        public Building GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return _ctx.Buildings.FirstOrDefault(b => b.Name == name);
        }

        public IReadOnlyList<Building> List()
            => _ctx.Buildings.OrderBy(b => b.Name).ToList().AsReadOnly();

        public Building Update(Building building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            _ctx.Entry(building).State = EntityState.Modified;
            _ctx.SaveChanges();
            return building;
        }

        public void Delete(int id)
        {
            var existing = _ctx.Buildings.Find(id);
            if (existing == null) return;
            _ctx.Buildings.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
