using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="ILoginAttemptRepository"/> для тестов
    /// и UI до подключения <see cref="Data.AhuDbContext"/>.
    /// </summary>
    public sealed class InMemoryLoginAttemptRepository : ILoginAttemptRepository
    {
        private readonly object _sync = new object();
        private readonly List<LoginAttempt> _attempts = new List<LoginAttempt>();
        private int _nextId = 1;

        public LoginAttempt Add(LoginAttempt attempt)
        {
            if (attempt == null) throw new ArgumentNullException(nameof(attempt));
            lock (_sync)
            {
                if (attempt.Id == 0) attempt.Id = _nextId++;
                else _nextId = Math.Max(_nextId, attempt.Id + 1);
                _attempts.Add(attempt);
                return attempt;
            }
        }

        public int CountRecentFailures(int employeeId, DateTime fromUtc)
        {
            lock (_sync)
            {
                return _attempts.Count(a =>
                    a.EmployeeId == employeeId
                    && !a.Success
                    && a.Timestamp >= fromUtc);
            }
        }

        public IReadOnlyList<LoginAttempt> Query(int? employeeId, DateTime? fromUtc, DateTime? toUtc, int limit)
        {
            lock (_sync)
            {
                IEnumerable<LoginAttempt> q = _attempts;
                if (employeeId.HasValue) q = q.Where(a => a.EmployeeId == employeeId.Value);
                if (fromUtc.HasValue) q = q.Where(a => a.Timestamp >= fromUtc.Value);
                if (toUtc.HasValue) q = q.Where(a => a.Timestamp <= toUtc.Value);
                if (limit <= 0) limit = 100;
                return q.OrderByDescending(a => a.Timestamp).Take(limit).ToList();
            }
        }
    }
}
