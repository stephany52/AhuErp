using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IEmployeeRepository"/>. Поиск по ФИО полагается на
    /// case-insensitive collation SQL Server (по умолчанию <c>SQL_Latin1_General_CP1_CI_AS</c>
    /// или аналогичная), что эквивалентно <see cref="StringComparison.OrdinalIgnoreCase"/>
    /// из in-memory реализации для типовых русских ФИО.
    /// </summary>
    public sealed class EfEmployeeRepository : IEmployeeRepository
    {
        private readonly AhuDbContext _ctx;

        public EfEmployeeRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public Employee FindByFullName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            return _ctx.Employees.FirstOrDefault(e => e.FullName == fullName);
        }

        public Employee GetById(int id) => _ctx.Employees.Find(id);

        public IReadOnlyList<Employee> ListAll()
            => _ctx.Employees.OrderBy(e => e.FullName).ToList().AsReadOnly();

        public void Save(Employee employee)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));

            // EF6 на singleton-контексте уже трекает изменения возвращённого
            // FindByFullName/Find экземпляра, так что SaveChanges достаточно.
            // Если по какой-то причине entry в Detached-состоянии — переводим
            // в Modified, чтобы изменения LockedUntil/LastPasswordChangeAt
            // долетели до БД.
            var entry = _ctx.Entry(employee);
            if (entry.State == System.Data.Entity.EntityState.Detached)
            {
                _ctx.Employees.Attach(employee);
                entry = _ctx.Entry(employee);
                entry.State = System.Data.Entity.EntityState.Modified;
            }
            _ctx.SaveChanges();
        }
    }
}
