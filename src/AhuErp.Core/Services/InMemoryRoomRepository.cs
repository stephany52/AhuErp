using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация <see cref="IRoomRepository"/> для тестов.</summary>
    public sealed class InMemoryRoomRepository : IRoomRepository
    {
        private readonly Dictionary<int, Room> _store = new Dictionary<int, Room>();
        private int _next = 1;

        public Room Add(Room room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (string.IsNullOrWhiteSpace(room.Number))
                throw new ArgumentException("Номер помещения обязателен.", nameof(room));
            if (room.BuildingId <= 0)
                throw new ArgumentException("Помещение должно принадлежать зданию.", nameof(room));

            if (_store.Values.Any(r => r.BuildingId == room.BuildingId && r.Number == room.Number))
                throw new InvalidOperationException(
                    $"Помещение с номером «{room.Number}» уже существует в выбранном здании.");

            room.Id = _next++;
            _store[room.Id] = room;
            return room;
        }

        public Room Get(int id) => _store.TryGetValue(id, out var r) ? r : null;

        public IReadOnlyList<Room> ListByBuilding(int buildingId)
            => _store.Values
                .Where(r => r.BuildingId == buildingId)
                .OrderBy(r => r.Floor)
                .ThenBy(r => r.Number)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Room> ListByPurpose(RoomPurpose purpose)
            => _store.Values
                .Where(r => r.Purpose == purpose)
                .OrderBy(r => r.BuildingId)
                .ThenBy(r => r.Number)
                .ToList()
                .AsReadOnly();

        public Room Update(Room room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (!_store.ContainsKey(room.Id))
                throw new InvalidOperationException("Помещение не найдено.");
            _store[room.Id] = room;
            return room;
        }

        public void Delete(int id) => _store.Remove(id);
    }
}
