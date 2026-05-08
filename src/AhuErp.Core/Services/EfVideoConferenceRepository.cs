using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="IVideoConferenceRepository"/>.</summary>
    public sealed class EfVideoConferenceRepository : IVideoConferenceRepository
    {
        private readonly AhuDbContext _ctx;

        public EfVideoConferenceRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public VideoConference Add(VideoConference conference)
        {
            if (conference == null) throw new ArgumentNullException(nameof(conference));
            if (string.IsNullOrWhiteSpace(conference.Topic))
                throw new ArgumentException("Тема ВКС обязательна.", nameof(conference));
            if (conference.OrganizerId <= 0)
                throw new ArgumentException("Не указан организатор.", nameof(conference));

            _ctx.VideoConferences.Add(conference);
            _ctx.SaveChanges();
            return conference;
        }

        public VideoConference Get(int id) => _ctx.VideoConferences.Find(id);

        public IReadOnlyList<VideoConference> List()
            => _ctx.VideoConferences.OrderByDescending(v => v.ScheduledAt).ToList().AsReadOnly();

        public IReadOnlyList<VideoConference> ListInRange(DateTime fromInclusive, DateTime toExclusive)
            => _ctx.VideoConferences
                .Where(v => v.ScheduledAt >= fromInclusive && v.ScheduledAt < toExclusive)
                .OrderBy(v => v.ScheduledAt)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<VideoConference> ListByOrganizer(int organizerId)
            => _ctx.VideoConferences.Where(v => v.OrganizerId == organizerId)
                .OrderByDescending(v => v.ScheduledAt)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<VideoConference> ListByTicket(int ticketId)
            => _ctx.VideoConferences.Where(v => v.TicketId == ticketId)
                .OrderByDescending(v => v.ScheduledAt)
                .ToList()
                .AsReadOnly();

        public VideoConference Update(VideoConference conference)
        {
            if (conference == null) throw new ArgumentNullException(nameof(conference));
            _ctx.Entry(conference).State = EntityState.Modified;
            _ctx.SaveChanges();
            return conference;
        }

        public void Delete(int id)
        {
            var existing = _ctx.VideoConferences.Find(id);
            if (existing == null) return;
            _ctx.VideoConferences.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
