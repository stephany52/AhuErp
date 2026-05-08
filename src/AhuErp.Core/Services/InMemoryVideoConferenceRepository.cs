using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация <see cref="IVideoConferenceRepository"/>.</summary>
    public sealed class InMemoryVideoConferenceRepository : IVideoConferenceRepository
    {
        private readonly List<VideoConference> _items = new List<VideoConference>();
        private int _nextId = 1;

        public VideoConference Add(VideoConference conference)
        {
            if (conference == null) throw new ArgumentNullException(nameof(conference));
            if (string.IsNullOrWhiteSpace(conference.Topic))
                throw new ArgumentException("Тема ВКС обязательна.", nameof(conference));
            if (conference.OrganizerId <= 0)
                throw new ArgumentException("Не указан организатор.", nameof(conference));

            if (conference.Id == 0) conference.Id = _nextId++;
            else _nextId = Math.Max(_nextId, conference.Id + 1);

            _items.Add(conference);
            return conference;
        }

        public VideoConference Get(int id) => _items.FirstOrDefault(v => v.Id == id);

        public IReadOnlyList<VideoConference> List()
            => _items.OrderByDescending(v => v.ScheduledAt).ToList().AsReadOnly();

        public IReadOnlyList<VideoConference> ListInRange(DateTime fromInclusive, DateTime toExclusive)
            => _items.Where(v => v.ScheduledAt >= fromInclusive && v.ScheduledAt < toExclusive)
                .OrderBy(v => v.ScheduledAt)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<VideoConference> ListByOrganizer(int organizerId)
            => _items.Where(v => v.OrganizerId == organizerId)
                .OrderByDescending(v => v.ScheduledAt)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<VideoConference> ListByTicket(int ticketId)
            => _items.Where(v => v.TicketId == ticketId)
                .OrderByDescending(v => v.ScheduledAt)
                .ToList()
                .AsReadOnly();

        public VideoConference Update(VideoConference conference)
        {
            if (conference == null) throw new ArgumentNullException(nameof(conference));
            var idx = _items.FindIndex(v => v.Id == conference.Id);
            if (idx < 0)
                throw new InvalidOperationException($"ВКС #{conference.Id} не найдена.");
            _items[idx] = conference;
            return conference;
        }

        public void Delete(int id)
        {
            var existing = _items.FirstOrDefault(v => v.Id == id);
            if (existing != null) _items.Remove(existing);
        }
    }
}
