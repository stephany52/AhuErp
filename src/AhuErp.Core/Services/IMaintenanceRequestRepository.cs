using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий заявок на эксплуатационные работы (Improvement #15 / Phase 18).
    /// </summary>
    public interface IMaintenanceRequestRepository
    {
        MaintenanceRequest Add(MaintenanceRequest request);
        MaintenanceRequest Get(int id);

        /// <summary>
        /// Список заявок с фильтрацией по периоду, зданию и статусу.
        /// Сортировка: открытые → в работе → закрытые, внутри — по приоритету
        /// убывающе и дате убывающе.
        /// </summary>
        IReadOnlyList<MaintenanceRequest> List(DateTime? from, DateTime? to,
            int? buildingId, MaintenanceStatus? status);

        IReadOnlyList<MaintenanceRequest> ListByAssignee(int employeeId);

        MaintenanceRequest Update(MaintenanceRequest request);
        void Delete(int id);
    }
}
