using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий реестра основных средств (Improvement #15 / Phase 18).
    /// Уникальность: <see cref="FixedAsset.InventoryNumber"/> в пределах учреждения.
    /// </summary>
    public interface IFixedAssetRepository
    {
        FixedAsset Add(FixedAsset asset);
        FixedAsset Get(int id);
        FixedAsset GetByInventoryNumber(string inventoryNumber);
        IReadOnlyList<FixedAsset> List();
        IReadOnlyList<FixedAsset> ListByCategory(FixedAssetCategory category);
        IReadOnlyList<FixedAsset> ListByStatus(FixedAssetStatus status);
        IReadOnlyList<FixedAsset> ListByResponsible(int employeeId);
        IReadOnlyList<FixedAsset> ListByBuilding(int buildingId);
        FixedAsset Update(FixedAsset asset);
        void Delete(int id);
    }
}
