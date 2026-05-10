using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="IRoomRepository"/>.</summary>
    public sealed class EfRoomRepository : IRoomRepository
    {
        private readonly AhuDbContext _ctx;

        public EfRoomRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public Room Add(Room room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (string.IsNullOrWhiteSpace(room.Number))
                throw new ArgumentException("Номер помещения обязателен.", nameof(room));
            if (room.BuildingId <= 0)
                throw new ArgumentException("Помещение должно принадлежать зданию.", nameof(room));

            if (_ctx.Rooms.Any(r => r.BuildingId == room.BuildingId && r.Number == room.Number))
                throw new InvalidOperationException(
                    $"Помещение с номером «{room.Number}» уже существует в выбранном здании.");

            _ctx.Rooms.Add(room);
            _ctx.SaveChanges();
            return room;
        }

        public Room Get(int id) => _ctx.Rooms.Find(id);

        public IReadOnlyList<Room> ListByBuilding(int buildingId)
            => _ctx.Rooms.Where(r => r.BuildingId == buildingId)
                .OrderBy(r => r.Floor)
                .ThenBy(r => r.Number)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Room> ListByPurpose(RoomPurpose purpose)
            => _ctx.Rooms.Where(r => r.Purpose == purpose)
                .OrderBy(r => r.BuildingId)
                .ThenBy(r => r.Number)
                .ToList()
                .AsReadOnly();

        public Room Update(Room room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            _ctx.Entry(room).State = EntityState.Modified;
            _ctx.SaveChanges();
            return room;
        }

        public void Delete(int id)
        {
            var existing = _ctx.Rooms.Find(id);
            if (existing == null) return;
            _ctx.Rooms.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
