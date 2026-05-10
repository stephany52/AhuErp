using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий зданий (Improvement #15 / Phase 18).
    /// Уникальность: <see cref="Building.Name"/> в пределах учреждения.
    /// </summary>
    public interface IBuildingRepository
    {
        Building Add(Building building);
        Building Get(int id);
        Building GetByName(string name);
        IReadOnlyList<Building> List();
        Building Update(Building building);
        void Delete(int id);
    }
}
