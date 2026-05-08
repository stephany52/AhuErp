using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий справочника сетевых сегментов (Phase 14 / Improvement #10).
    /// </summary>
    public interface INetworkSegmentRepository
    {
        NetworkSegment Add(NetworkSegment segment);
        NetworkSegment Get(int id);
        IReadOnlyList<NetworkSegment> List();
        NetworkSegment Update(NetworkSegment segment);
        void Delete(int id);
    }
}
