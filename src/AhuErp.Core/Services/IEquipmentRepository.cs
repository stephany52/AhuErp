using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий каталога оборудования ИТО (Phase 14 / Improvement #10).
    /// </summary>
    public interface IEquipmentRepository
    {
        Equipment Add(Equipment equipment);
        Equipment Get(int id);
        Equipment GetByInventoryNumber(string inventoryNumber);
        IReadOnlyList<Equipment> List();
        IReadOnlyList<Equipment> ListByStatus(EquipmentStatus status);
        IReadOnlyList<Equipment> ListByResponsible(int employeeId);
        IReadOnlyList<Equipment> ListBySegment(int networkSegmentId);
        Equipment Update(Equipment equipment);
        void Delete(int id);
    }
}
