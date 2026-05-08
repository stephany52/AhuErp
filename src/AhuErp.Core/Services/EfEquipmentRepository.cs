using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="IEquipmentRepository"/>.</summary>
    public sealed class EfEquipmentRepository : IEquipmentRepository
    {
        private readonly AhuDbContext _ctx;

        public EfEquipmentRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public Equipment Add(Equipment equipment)
        {
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            if (string.IsNullOrWhiteSpace(equipment.InventoryNumber))
                throw new ArgumentException("Инвентарный номер обязателен.", nameof(equipment));

            if (_ctx.Equipment.Any(e => e.InventoryNumber == equipment.InventoryNumber))
                throw new InvalidOperationException(
                    $"Оборудование с инв. номером «{equipment.InventoryNumber}» уже зарегистрировано.");

            _ctx.Equipment.Add(equipment);
            _ctx.SaveChanges();
            return equipment;
        }

        public Equipment Get(int id) => _ctx.Equipment.Find(id);

        public Equipment GetByInventoryNumber(string inventoryNumber)
        {
            if (string.IsNullOrWhiteSpace(inventoryNumber)) return null;
            return _ctx.Equipment.FirstOrDefault(e => e.InventoryNumber == inventoryNumber);
        }

        public IReadOnlyList<Equipment> List()
            => _ctx.Equipment.OrderBy(e => e.InventoryNumber).ToList().AsReadOnly();

        public IReadOnlyList<Equipment> ListByStatus(EquipmentStatus status)
            => _ctx.Equipment.Where(e => e.Status == status)
                .OrderBy(e => e.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Equipment> ListByResponsible(int employeeId)
            => _ctx.Equipment.Where(e => e.ResponsibleEmployeeId == employeeId)
                .OrderBy(e => e.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Equipment> ListBySegment(int networkSegmentId)
            => _ctx.Equipment.Where(e => e.NetworkSegmentId == networkSegmentId)
                .OrderBy(e => e.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public Equipment Update(Equipment equipment)
        {
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            _ctx.Entry(equipment).State = EntityState.Modified;
            _ctx.SaveChanges();
            return equipment;
        }

        public void Delete(int id)
        {
            var existing = _ctx.Equipment.Find(id);
            if (existing == null) return;
            _ctx.Equipment.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
