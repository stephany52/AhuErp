using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6 реализация <see cref="IEmployeePasswordHistoryRepository"/>.
    /// </summary>
    public sealed class EfEmployeePasswordHistoryRepository : IEmployeePasswordHistoryRepository
    {
        private readonly Func<AhuDbContext> _contextFactory;

        public EfEmployeePasswordHistoryRepository(Func<AhuDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public IReadOnlyList<EmployeePasswordHistory> ListForEmployee(int employeeId)
        {
            using (var ctx = _contextFactory())
            {
                return ctx.EmployeePasswordHistories.AsNoTracking()
                    .Where(e => e.EmployeeId == employeeId)
                    .OrderByDescending(e => e.SetAt)
                    .ToList();
            }
        }

        public EmployeePasswordHistory Add(EmployeePasswordHistory entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            using (var ctx = _contextFactory())
            {
                ctx.EmployeePasswordHistories.Add(entry);
                ctx.SaveChanges();
                return entry;
            }
        }

        public void TrimToDepth(int employeeId, int depth)
        {
            if (depth <= 0) return;
            using (var ctx = _contextFactory())
            {
                var stale = ctx.EmployeePasswordHistories
                    .Where(e => e.EmployeeId == employeeId)
                    .OrderByDescending(e => e.SetAt)
                    .Skip(depth)
                    .ToList();

                if (stale.Count == 0) return;
                foreach (var s in stale) ctx.EmployeePasswordHistories.Remove(s);
                ctx.SaveChanges();
            }
        }
    }
}
