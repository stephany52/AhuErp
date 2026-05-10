using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация <see cref="IFixedAssetRepository"/>.</summary>
    public sealed class InMemoryFixedAssetRepository : IFixedAssetRepository
    {
        private readonly Dictionary<int, FixedAsset> _store = new Dictionary<int, FixedAsset>();
        private int _next = 1;

        public FixedAsset Add(FixedAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (string.IsNullOrWhiteSpace(asset.InventoryNumber))
                throw new ArgumentException("Инвентарный номер обязателен.", nameof(asset));
            if (string.IsNullOrWhiteSpace(asset.Name))
                throw new ArgumentException("Наименование основного средства обязательно.", nameof(asset));
            if (_store.Values.Any(a => a.InventoryNumber == asset.InventoryNumber))
                throw new InvalidOperationException(
                    $"Основное средство с инвентарным номером «{asset.InventoryNumber}» уже зарегистрировано.");

            asset.Id = _next++;
            _store[asset.Id] = asset;
            return asset;
        }

        public FixedAsset Get(int id) => _store.TryGetValue(id, out var a) ? a : null;

        public FixedAsset GetByInventoryNumber(string inventoryNumber)
        {
            if (string.IsNullOrWhiteSpace(inventoryNumber)) return null;
            return _store.Values.FirstOrDefault(a => a.InventoryNumber == inventoryNumber);
        }

        public IReadOnlyList<FixedAsset> List()
            => _store.Values
                .OrderBy(a => a.Category)
                .ThenBy(a => a.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<FixedAsset> ListByCategory(FixedAssetCategory category)
            => _store.Values
                .Where(a => a.Category == category)
                .OrderBy(a => a.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<FixedAsset> ListByStatus(FixedAssetStatus status)
            => _store.Values
                .Where(a => a.Status == status)
                .OrderBy(a => a.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<FixedAsset> ListByResponsible(int employeeId)
            => _store.Values
                .Where(a => a.ResponsibleEmployeeId == employeeId)
                .OrderBy(a => a.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<FixedAsset> ListByBuilding(int buildingId)
            => _store.Values
                .Where(a => a.BuildingId == buildingId)
                .OrderBy(a => a.RoomId)
                .ThenBy(a => a.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public FixedAsset Update(FixedAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (!_store.ContainsKey(asset.Id))
                throw new InvalidOperationException("Основное средство не найдено.");
            _store[asset.Id] = asset;
            return asset;
        }

        public void Delete(int id) => _store.Remove(id);
    }
}
