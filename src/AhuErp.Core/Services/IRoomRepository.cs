using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий помещений (Improvement #15 / Phase 18). Уникальность номера
    /// помещения обеспечивается в пределах одного здания, см. также
    /// <see cref="Room.Number"/>.
    /// </summary>
    public interface IRoomRepository
    {
        Room Add(Room room);
        Room Get(int id);
        IReadOnlyList<Room> ListByBuilding(int buildingId);
        IReadOnlyList<Room> ListByPurpose(RoomPurpose purpose);
        Room Update(Room room);
        void Delete(int id);
    }
}
