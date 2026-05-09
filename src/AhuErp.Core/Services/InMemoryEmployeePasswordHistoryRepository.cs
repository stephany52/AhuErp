using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IEmployeePasswordHistoryRepository"/>.
    /// </summary>
    public sealed class InMemoryEmployeePasswordHistoryRepository : IEmployeePasswordHistoryRepository
    {
        private readonly object _sync = new object();
        private readonly List<EmployeePasswordHistory> _entries = new List<EmployeePasswordHistory>();
        private int _nextId = 1;

        public IReadOnlyList<EmployeePasswordHistory> ListForEmployee(int employeeId)
        {
            lock (_sync)
            {
                return _entries
                    .Where(e => e.EmployeeId == employeeId)
                    .OrderByDescending(e => e.SetAt)
                    .ToList();
            }
        }

        public EmployeePasswordHistory Add(EmployeePasswordHistory entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            lock (_sync)
            {
                if (entry.Id == 0) entry.Id = _nextId++;
                else _nextId = Math.Max(_nextId, entry.Id + 1);
                _entries.Add(entry);
                return entry;
            }
        }

        public void TrimToDepth(int employeeId, int depth)
        {
            if (depth <= 0) return;
            lock (_sync)
            {
                var sorted = _entries
                    .Where(e => e.EmployeeId == employeeId)
                    .OrderByDescending(e => e.SetAt)
                    .Skip(depth)
                    .ToList();

                foreach (var stale in sorted) _entries.Remove(stale);
            }
        }
    }
}
