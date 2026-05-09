using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6 реализация <see cref="ILoginAttemptRepository"/>.
    /// </summary>
    public sealed class EfLoginAttemptRepository : ILoginAttemptRepository
    {
        private readonly Func<AhuDbContext> _contextFactory;

        public EfLoginAttemptRepository(Func<AhuDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public LoginAttempt Add(LoginAttempt attempt)
        {
            if (attempt == null) throw new ArgumentNullException(nameof(attempt));
            using (var ctx = _contextFactory())
            {
                ctx.LoginAttempts.Add(attempt);
                ctx.SaveChanges();
                return attempt;
            }
        }

        public int CountRecentFailures(int employeeId, DateTime fromUtc)
        {
            using (var ctx = _contextFactory())
            {
                return ctx.LoginAttempts.AsNoTracking()
                    .Count(a => a.EmployeeId == employeeId
                                && !a.Success
                                && a.Timestamp >= fromUtc);
            }
        }

        public IReadOnlyList<LoginAttempt> Query(int? employeeId, DateTime? fromUtc, DateTime? toUtc, int limit)
        {
            using (var ctx = _contextFactory())
            {
                IQueryable<LoginAttempt> q = ctx.LoginAttempts.AsNoTracking();
                if (employeeId.HasValue) q = q.Where(a => a.EmployeeId == employeeId.Value);
                if (fromUtc.HasValue) q = q.Where(a => a.Timestamp >= fromUtc.Value);
                if (toUtc.HasValue) q = q.Where(a => a.Timestamp <= toUtc.Value);
                if (limit <= 0) limit = 100;
                return q.OrderByDescending(a => a.Timestamp).Take(limit).ToList();
            }
        }
    }
}
