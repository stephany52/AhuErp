using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация <see cref="INetworkSegmentRepository"/>.</summary>
    public sealed class InMemoryNetworkSegmentRepository : INetworkSegmentRepository
    {
        private readonly List<NetworkSegment> _items = new List<NetworkSegment>();
        private int _nextId = 1;

        public NetworkSegment Add(NetworkSegment segment)
        {
            if (segment == null) throw new ArgumentNullException(nameof(segment));
            if (string.IsNullOrWhiteSpace(segment.Name))
                throw new ArgumentException("Название сегмента обязательно.", nameof(segment));

            if (segment.Id == 0) segment.Id = _nextId++;
            else _nextId = Math.Max(_nextId, segment.Id + 1);

            _items.Add(segment);
            return segment;
        }

        public NetworkSegment Get(int id) => _items.FirstOrDefault(s => s.Id == id);

        public IReadOnlyList<NetworkSegment> List()
            => _items.OrderBy(s => s.Name).ToList().AsReadOnly();

        public NetworkSegment Update(NetworkSegment segment)
        {
            if (segment == null) throw new ArgumentNullException(nameof(segment));
            var idx = _items.FindIndex(s => s.Id == segment.Id);
            if (idx < 0)
                throw new InvalidOperationException($"Сегмент сети #{segment.Id} не найден.");
            _items[idx] = segment;
            return segment;
        }

        public void Delete(int id)
        {
            var existing = _items.FirstOrDefault(s => s.Id == id);
            if (existing != null) _items.Remove(existing);
        }
    }
}
