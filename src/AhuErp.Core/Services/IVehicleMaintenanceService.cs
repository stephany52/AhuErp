using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 17 / Improvement #14 — фасад над <see cref="IVehicleRepository"/>
    /// и <see cref="INotificationService"/>: обходит автопарк и уведомляет
    /// ответственных (WarehouseManager + Admin) о приближении сроков ОСАГО,
    /// технического осмотра и планового ТО по пробегу.
    /// </summary>
    public interface IVehicleMaintenanceService
    {
        /// <summary>
        /// Идемпотентный обход всех ТС: создаёт уведомления
        /// <see cref="NotificationKind.VehicleOsagoExpiringSoon"/>,
        /// <see cref="NotificationKind.VehicleTechInspectionExpiringSoon"/>
        /// и <see cref="NotificationKind.VehicleMaintenanceDueSoon"/>
        /// для всех получателей с ролью
        /// <see cref="EmployeeRole.WarehouseManager"/> /
        /// <see cref="EmployeeRole.FleetManager"/> / <see cref="EmployeeRole.Admin"/>.
        /// Повторный вызов в пределах окна предупреждения записей не дублирует.
        /// </summary>
        /// <param name="now">Текущее время (для тестируемости).</param>
        /// <param name="daysAhead">Окно предупреждения для ОСАГО / ТО (по умолчанию 30).</param>
        /// <param name="kmAhead">
        /// Окно предупреждения для планового ТО по пробегу: уведомление
        /// шлётся, если разница <see cref="Vehicle.NextMaintenanceOdometer"/> −
        /// <see cref="Vehicle.OdometerCurrent"/> &lt;= этого значения.
        /// </param>
        /// <returns>Список созданных уведомлений (диагностика для UI/тестов).</returns>
        IReadOnlyList<Notification> CheckExpiringDocuments(DateTime now, int daysAhead = 30, int kmAhead = 1000);
    }
}
