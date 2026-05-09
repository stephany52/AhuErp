using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Абстракция доступа к сотрудникам. В проде привязывается к EF6,
    /// в тестах — к in-memory реализации (паттерн, уже применённый в Phase 1).
    /// </summary>
    public interface IEmployeeRepository
    {
        /// <summary>
        /// Возвращает сотрудника по уникальному ФИО (Phase 2 — упрощённая схема,
        /// в Phase 5 может быть заменена на логин/email). Возвращает <c>null</c>,
        /// если сотрудника нет.
        /// </summary>
        Employee FindByFullName(string fullName);

        /// <summary>Сотрудник по идентификатору; <c>null</c>, если не найден.</summary>
        Employee GetById(int id);

        /// <summary>
        /// Все сотрудники (Phase 11 — для UI оргструктуры и замещений).
        /// </summary>
        IReadOnlyList<Employee> ListAll();

        /// <summary>
        /// Сохраняет изменения существующего сотрудника (Phase 16 — для
        /// фиксирования <see cref="Employee.LockedUntil"/> и
        /// <see cref="Employee.LastPasswordChangeAt"/> из <c>AuthService</c>).
        /// Реализации обязаны быть идемпотентными для in-memory объекта,
        /// уже находящегося в коллекции.
        /// </summary>
        void Save(Employee employee);
    }
}
