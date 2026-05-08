using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="INetworkSegmentRepository"/>.</summary>
    public sealed class EfNetworkSegmentRepository : INetworkSegmentRepository
    {
        private readonly AhuDbContext _ctx;

        public EfNetworkSegmentRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public NetworkSegment Add(NetworkSegment segment)
        {
            if (segment == null) throw new ArgumentNullException(nameof(segment));
            if (string.IsNullOrWhiteSpace(segment.Name))
                throw new ArgumentException("Название сегмента обязательно.", nameof(segment));

            _ctx.NetworkSegments.Add(segment);
            _ctx.SaveChanges();
            return segment;
        }

        public NetworkSegment Get(int id) => _ctx.NetworkSegments.Find(id);

        public IReadOnlyList<NetworkSegment> List()
            => _ctx.NetworkSegments.OrderBy(s => s.Name).ToList().AsReadOnly();

        public NetworkSegment Update(NetworkSegment segment)
        {
            if (segment == null) throw new ArgumentNullException(nameof(segment));
            _ctx.Entry(segment).State = EntityState.Modified;
            _ctx.SaveChanges();
            return segment;
        }

        public void Delete(int id)
        {
            var existing = _ctx.NetworkSegments.Find(id);
            if (existing == null) return;
            _ctx.NetworkSegments.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
