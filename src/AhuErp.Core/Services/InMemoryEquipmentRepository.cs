using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IEquipmentRepository"/> — для тестов
    /// и демо-режима, где AhuDbContext не используется (Phase 14).
    /// </summary>
    public sealed class InMemoryEquipmentRepository : IEquipmentRepository
    {
        private readonly List<Equipment> _items = new List<Equipment>();
        private int _nextId = 1;

        public Equipment Add(Equipment equipment)
        {
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            if (string.IsNullOrWhiteSpace(equipment.InventoryNumber))
                throw new ArgumentException("Инвентарный номер обязателен.", nameof(equipment));
            if (_items.Any(e => string.Equals(e.InventoryNumber, equipment.InventoryNumber, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    $"Оборудование с инв. номером «{equipment.InventoryNumber}» уже зарегистрировано.");

            if (equipment.Id == 0) equipment.Id = _nextId++;
            else _nextId = Math.Max(_nextId, equipment.Id + 1);

            _items.Add(equipment);
            return equipment;
        }

        public Equipment Get(int id) => _items.FirstOrDefault(e => e.Id == id);

        public Equipment GetByInventoryNumber(string inventoryNumber)
        {
            if (string.IsNullOrWhiteSpace(inventoryNumber)) return null;
            return _items.FirstOrDefault(e =>
                string.Equals(e.InventoryNumber, inventoryNumber, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<Equipment> List()
            => _items.OrderBy(e => e.InventoryNumber).ToList().AsReadOnly();

        public IReadOnlyList<Equipment> ListByStatus(EquipmentStatus status)
            => _items.Where(e => e.Status == status)
                .OrderBy(e => e.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Equipment> ListByResponsible(int employeeId)
            => _items.Where(e => e.ResponsibleEmployeeId == employeeId)
                .OrderBy(e => e.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Equipment> ListBySegment(int networkSegmentId)
            => _items.Where(e => e.NetworkSegmentId == networkSegmentId)
                .OrderBy(e => e.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public Equipment Update(Equipment equipment)
        {
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            var idx = _items.FindIndex(e => e.Id == equipment.Id);
            if (idx < 0)
                throw new InvalidOperationException($"Оборудование #{equipment.Id} не найдено.");
            _items[idx] = equipment;
            return equipment;
        }

        public void Delete(int id)
        {
            var existing = _items.FirstOrDefault(e => e.Id == id);
            if (existing != null) _items.Remove(existing);
        }
    }
}
