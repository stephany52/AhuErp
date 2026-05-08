using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий журнала видеоконференций (Phase 14 / Improvement #10).
    /// </summary>
    public interface IVideoConferenceRepository
    {
        VideoConference Add(VideoConference conference);
        VideoConference Get(int id);
        IReadOnlyList<VideoConference> List();
        IReadOnlyList<VideoConference> ListInRange(DateTime fromInclusive, DateTime toExclusive);
        IReadOnlyList<VideoConference> ListByOrganizer(int organizerId);
        IReadOnlyList<VideoConference> ListByTicket(int ticketId);
        VideoConference Update(VideoConference conference);
        void Delete(int id);
    }
}
