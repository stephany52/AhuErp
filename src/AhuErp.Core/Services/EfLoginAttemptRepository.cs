using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6 реализация <see cref="ILoginAttemptRepository"/>. Использует
    /// единый singleton-контекст <see cref="AhuDbContext"/>, как и остальные
    /// EF-репозитории проекта (см. <see cref="EfEmployeeRepository"/>) —
    /// MS DI не резолвит <c>Func&lt;T&gt;</c> автоматически, а контекст
    /// уже зарегистрирован как singleton с UI-потоковым доступом.
    /// </summary>
    public sealed class EfLoginAttemptRepository : ILoginAttemptRepository
    {
        private readonly AhuDbContext _ctx;

        public EfLoginAttemptRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public LoginAttempt Add(LoginAttempt attempt)
        {
            if (attempt == null) throw new ArgumentNullException(nameof(attempt));
            _ctx.LoginAttempts.Add(attempt);
            _ctx.SaveChanges();
            return attempt;
        }

        public int CountRecentFailures(int employeeId, DateTime fromUtc)
        {
            return _ctx.LoginAttempts.AsNoTracking()
                .Count(a => a.EmployeeId == employeeId
                            && !a.Success
                            && a.Timestamp >= fromUtc);
        }

        public IReadOnlyList<LoginAttempt> Query(int? employeeId, DateTime? fromUtc, DateTime? toUtc, int limit)
        {
            IQueryable<LoginAttempt> q = _ctx.LoginAttempts.AsNoTracking();
            if (employeeId.HasValue) q = q.Where(a => a.EmployeeId == employeeId.Value);
            if (fromUtc.HasValue) q = q.Where(a => a.Timestamp >= fromUtc.Value);
            if (toUtc.HasValue) q = q.Where(a => a.Timestamp <= toUtc.Value);
            if (limit <= 0) limit = 100;
            return q.OrderByDescending(a => a.Timestamp).Take(limit).ToList();
        }
    }
}
