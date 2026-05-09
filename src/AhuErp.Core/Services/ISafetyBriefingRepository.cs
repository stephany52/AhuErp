using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Хранилище журнала инструктажей по охране труда / пожарной безопасности.
    /// Improvement #12 / Phase 15.
    /// </summary>
    public interface ISafetyBriefingRepository
    {
        IReadOnlyList<SafetyBriefing> List(DateTime? from, DateTime? to, BriefingKind? kind);
        SafetyBriefing GetById(int id);
        void Add(SafetyBriefing briefing);
        void Update(SafetyBriefing briefing);
        void Remove(int id);
    }
}
