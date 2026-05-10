using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис управления зданиями и помещениями (Phase 18 / Improvement #15).
    /// Тонкая надстройка над <see cref="IBuildingRepository"/> и
    /// <see cref="IRoomRepository"/>: централизует валидацию и аудит CRUD-операций
    /// для UI и других сервисов.
    /// </summary>
    public interface IBuildingService
    {
        /// <summary>Регистрирует новое здание; возвращает запись с присвоенным Id.</summary>
        Building RegisterBuilding(Building building, int actorId);

        /// <summary>Обновляет существующее здание; пишет аудит-запись.</summary>
        Building UpdateBuilding(Building building, int actorId);

        /// <summary>Возвращает здание по идентификатору либо <c>null</c>.</summary>
        Building GetBuilding(int id);

        /// <summary>Список всех зданий, сортированный по наименованию.</summary>
        IReadOnlyList<Building> ListBuildings();

        /// <summary>Регистрирует новое помещение в указанном здании; пишет аудит.</summary>
        Room AddRoom(Room room, int actorId);

        /// <summary>Обновляет существующее помещение; пишет аудит.</summary>
        Room UpdateRoom(Room room, int actorId);

        /// <summary>Список помещений конкретного здания.</summary>
        IReadOnlyList<Room> ListRooms(int buildingId);
    }
}
