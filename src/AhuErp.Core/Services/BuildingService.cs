using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IBuildingService"/>. Обёртка над репозиториями
    /// зданий и помещений с записью аудит-журнала.
    /// </summary>
    public sealed class BuildingService : IBuildingService
    {
        private readonly IBuildingRepository _buildingRepo;
        private readonly IRoomRepository _roomRepo;
        private readonly IAuditService _audit;

        public BuildingService(IBuildingRepository buildingRepo, IRoomRepository roomRepo,
            IAuditService audit)
        {
            _buildingRepo = buildingRepo ?? throw new ArgumentNullException(nameof(buildingRepo));
            _roomRepo = roomRepo ?? throw new ArgumentNullException(nameof(roomRepo));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public Building RegisterBuilding(Building building, int actorId)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            var saved = _buildingRepo.Add(building);
            _audit.Record(AuditActionType.BuildingCreated, "Building", saved.Id, actorId,
                details: $"Зарегистрировано здание «{saved.Name}».");
            return saved;
        }

        public Building UpdateBuilding(Building building, int actorId)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            var saved = _buildingRepo.Update(building);
            _audit.Record(AuditActionType.BuildingUpdated, "Building", saved.Id, actorId,
                details: $"Обновлены данные здания «{saved.Name}».");
            return saved;
        }

        public Building GetBuilding(int id) => _buildingRepo.Get(id);

        public IReadOnlyList<Building> ListBuildings() => _buildingRepo.List();

        public Room AddRoom(Room room, int actorId)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (_buildingRepo.Get(room.BuildingId) == null)
                throw new InvalidOperationException("Здание для помещения не найдено.");
            var saved = _roomRepo.Add(room);
            _audit.Record(AuditActionType.RoomCreated, "Room", saved.Id, actorId,
                details: $"Добавлено помещение №{saved.Number} в здании #{saved.BuildingId}.");
            return saved;
        }

        public Room UpdateRoom(Room room, int actorId)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            var saved = _roomRepo.Update(room);
            _audit.Record(AuditActionType.RoomUpdated, "Room", saved.Id, actorId,
                details: $"Обновлены данные помещения №{saved.Number}.");
            return saved;
        }

        public IReadOnlyList<Room> ListRooms(int buildingId)
            => _roomRepo.ListByBuilding(buildingId);
    }
}
