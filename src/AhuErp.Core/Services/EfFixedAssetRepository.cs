using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="IFixedAssetRepository"/>.</summary>
    public sealed class EfFixedAssetRepository : IFixedAssetRepository
    {
        private readonly AhuDbContext _ctx;

        public EfFixedAssetRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public FixedAsset Add(FixedAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (string.IsNullOrWhiteSpace(asset.InventoryNumber))
                throw new ArgumentException("Инвентарный номер обязателен.", nameof(asset));
            if (string.IsNullOrWhiteSpace(asset.Name))
                throw new ArgumentException("Наименование основного средства обязательно.", nameof(asset));

            if (_ctx.FixedAssets.Any(a => a.InventoryNumber == asset.InventoryNumber))
                throw new InvalidOperationException(
                    $"Основное средство с инвентарным номером «{asset.InventoryNumber}» уже зарегистрировано.");

            _ctx.FixedAssets.Add(asset);
            _ctx.SaveChanges();
            return asset;
        }

        public FixedAsset Get(int id) => _ctx.FixedAssets.Find(id);

        public FixedAsset GetByInventoryNumber(string inventoryNumber)
        {
            if (string.IsNullOrWhiteSpace(inventoryNumber)) return null;
            return _ctx.FixedAssets.FirstOrDefault(a => a.InventoryNumber == inventoryNumber);
        }

        public IReadOnlyList<FixedAsset> List()
            => _ctx.FixedAssets
                .OrderBy(a => a.Category)
                .ThenBy(a => a.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<FixedAsset> ListByCategory(FixedAssetCategory category)
            => _ctx.FixedAssets
                .Where(a => a.Category == category)
                .OrderBy(a => a.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<FixedAsset> ListByStatus(FixedAssetStatus status)
            => _ctx.FixedAssets
                .Where(a => a.Status == status)
                .OrderBy(a => a.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<FixedAsset> ListByResponsible(int employeeId)
            => _ctx.FixedAssets
                .Where(a => a.ResponsibleEmployeeId == employeeId)
                .OrderBy(a => a.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<FixedAsset> ListByBuilding(int buildingId)
            => _ctx.FixedAssets
                .Where(a => a.BuildingId == buildingId)
                .OrderBy(a => a.RoomId)
                .ThenBy(a => a.InventoryNumber)
                .ToList()
                .AsReadOnly();

        public FixedAsset Update(FixedAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            _ctx.Entry(asset).State = EntityState.Modified;
            _ctx.SaveChanges();
            return asset;
        }

        public void Delete(int id)
        {
            var existing = _ctx.FixedAssets.Find(id);
            if (existing == null) return;
            _ctx.FixedAssets.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
