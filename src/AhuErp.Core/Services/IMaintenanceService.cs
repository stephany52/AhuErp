using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис заявок на эксплуатационные работы (Phase 18 / Improvement #15).
    /// Управляет жизненным циклом <see cref="MaintenanceRequest"/>:
    /// <c>Open → InProgress → Completed | Cancelled</c>.
    /// </summary>
    public interface IMaintenanceService
    {
        /// <summary>Регистрирует новую заявку (статус <see cref="MaintenanceStatus.Open"/>).</summary>
        MaintenanceRequest CreateRequest(MaintenanceRequest request, int actorId);

        /// <summary>Назначает исполнителя; статус переводит в <see cref="MaintenanceStatus.InProgress"/>.</summary>
        /// <exception cref="InvalidOperationException">
        /// Заявка уже завершена/отменена либо некорректное состояние.
        /// </exception>
        MaintenanceRequest Assign(int requestId, int assigneeEmployeeId, int actorId);

        /// <summary>
        /// Закрывает заявку с указанием результата работы. Переход допустим из
        /// <see cref="MaintenanceStatus.Open"/> или <see cref="MaintenanceStatus.InProgress"/>.
        /// </summary>
        MaintenanceRequest Complete(int requestId, string resolution, int actorId, DateTime? now = null);

        /// <summary>Отменяет заявку (терминальный статус).</summary>
        MaintenanceRequest Cancel(int requestId, string reason, int actorId, DateTime? now = null);

        /// <summary>Список заявок с фильтрацией.</summary>
        IReadOnlyList<MaintenanceRequest> ListRequests(DateTime? from, DateTime? to,
            int? buildingId, MaintenanceStatus? status);
    }
}
