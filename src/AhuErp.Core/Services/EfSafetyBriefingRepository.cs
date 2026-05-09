using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="ISafetyBriefingRepository"/> поверх <see cref="AhuDbContext"/>.
    /// </summary>
    public sealed class EfSafetyBriefingRepository : ISafetyBriefingRepository
    {
        private readonly AhuDbContext _ctx;

        public EfSafetyBriefingRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public IReadOnlyList<SafetyBriefing> List(DateTime? from, DateTime? to, BriefingKind? kind)
        {
            IQueryable<SafetyBriefing> q = _ctx.SafetyBriefings
                .Include(b => b.TraineeEmployee)
                .Include(b => b.InstructorEmployee);
            if (from.HasValue) q = q.Where(b => b.BriefingDate >= from.Value);
            if (to.HasValue) q = q.Where(b => b.BriefingDate <= to.Value);
            if (kind.HasValue) q = q.Where(b => b.Kind == kind.Value);
            return q.OrderByDescending(b => b.BriefingDate)
                    .ThenBy(b => b.Id)
                    .ToList()
                    .AsReadOnly();
        }

        public SafetyBriefing GetById(int id) => _ctx.SafetyBriefings.Find(id);

        public void Add(SafetyBriefing briefing)
        {
            if (briefing == null) throw new ArgumentNullException(nameof(briefing));
            _ctx.SafetyBriefings.Add(briefing);
            _ctx.SaveChanges();
        }

        public void Update(SafetyBriefing briefing)
        {
            if (briefing == null) throw new ArgumentNullException(nameof(briefing));
            _ctx.Entry(briefing).State = EntityState.Modified;
            _ctx.SaveChanges();
        }

        public void Remove(int id)
        {
            var existing = _ctx.SafetyBriefings.Find(id);
            if (existing == null) return;
            _ctx.SafetyBriefings.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
