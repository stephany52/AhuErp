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
    /// Принимает singleton-<see cref="AhuDbContext"/> напрямую (как и остальные
    /// EF-репозитории проекта), MS DI не резолвит <c>Func&lt;T&gt;</c>.
    /// </summary>
    public sealed class EfEmployeePasswordHistoryRepository : IEmployeePasswordHistoryRepository
    {
        private readonly AhuDbContext _ctx;

        public EfEmployeePasswordHistoryRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public IReadOnlyList<EmployeePasswordHistory> ListForEmployee(int employeeId)
        {
            return _ctx.EmployeePasswordHistories.AsNoTracking()
                .Where(e => e.EmployeeId == employeeId)
                .OrderByDescending(e => e.SetAt)
                .ToList();
        }

        public EmployeePasswordHistory Add(EmployeePasswordHistory entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            _ctx.EmployeePasswordHistories.Add(entry);
            _ctx.SaveChanges();
            return entry;
        }

        public void TrimToDepth(int employeeId, int depth)
        {
            if (depth <= 0) return;
            var stale = _ctx.EmployeePasswordHistories
                .Where(e => e.EmployeeId == employeeId)
                .OrderByDescending(e => e.SetAt)
                .Skip(depth)
                .ToList();

            if (stale.Count == 0) return;
            foreach (var s in stale) _ctx.EmployeePasswordHistories.Remove(s);
            _ctx.SaveChanges();
        }
    }
}
