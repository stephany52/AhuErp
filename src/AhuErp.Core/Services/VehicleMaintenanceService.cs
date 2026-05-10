using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IVehicleMaintenanceService"/>. Обходит автопарк
    /// и формирует уведомления о приближении сроков ОСАГО / технического
    /// осмотра / планового ТО по пробегу. Идемпотентен: повторный обход в
    /// пределах окна предупреждения не дублирует уведомления.
    /// </summary>
    public sealed class VehicleMaintenanceService : IVehicleMaintenanceService
    {
        private readonly IVehicleRepository _vehicles;
        private readonly INotificationService _notifications;
        private readonly IEmployeeRepository _employees;
        private readonly INotificationRepository _notificationRepo;

        public VehicleMaintenanceService(
            IVehicleRepository vehicles,
            INotificationService notifications,
            IEmployeeRepository employees,
            INotificationRepository notificationRepo)
        {
            _vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _employees = employees ?? throw new ArgumentNullException(nameof(employees));
            _notificationRepo = notificationRepo ?? throw new ArgumentNullException(nameof(notificationRepo));
        }

        public IReadOnlyList<Notification> CheckExpiringDocuments(
            DateTime now, int daysAhead = 30, int kmAhead = 1000)
        {
            if (daysAhead < 0) throw new ArgumentOutOfRangeException(nameof(daysAhead));
            if (kmAhead < 0) throw new ArgumentOutOfRangeException(nameof(kmAhead));

            var recipients = _employees.ListAll()
                .Where(e => e.Role == EmployeeRole.WarehouseManager
                            || e.Role == EmployeeRole.FleetManager
                            || e.Role == EmployeeRole.Admin)
                .ToList();
            if (recipients.Count == 0) return Array.Empty<Notification>();

            var created = new List<Notification>();

            foreach (var v in _vehicles.ListVehicles())
            {
                if (v.OsagoExpiry.HasValue
                    && (v.OsagoExpiry.Value - now).TotalDays <= daysAhead)
                {
                    EnqueueForAll(v, NotificationKind.VehicleOsagoExpiringSoon,
                        $"ОСАГО {v.Make} {v.Model} ({v.LicensePlate}) истекает {v.OsagoExpiry:dd.MM.yyyy}",
                        $"Срок действия ОСАГО ТС «{v.Make} {v.Model}» (гос. номер {v.LicensePlate}) истекает {v.OsagoExpiry:dd.MM.yyyy}. До истечения: {Math.Max(0, (int)Math.Ceiling((v.OsagoExpiry.Value - now).TotalDays))} дн.",
                        recipients, created, now);
                }

                if (v.TechInspectionExpiry.HasValue
                    && (v.TechInspectionExpiry.Value - now).TotalDays <= daysAhead)
                {
                    EnqueueForAll(v, NotificationKind.VehicleTechInspectionExpiringSoon,
                        $"ТО {v.Make} {v.Model} ({v.LicensePlate}) истекает {v.TechInspectionExpiry:dd.MM.yyyy}",
                        $"Срок действия диагностической карты ТС «{v.Make} {v.Model}» (гос. номер {v.LicensePlate}) истекает {v.TechInspectionExpiry:dd.MM.yyyy}. До истечения: {Math.Max(0, (int)Math.Ceiling((v.TechInspectionExpiry.Value - now).TotalDays))} дн.",
                        recipients, created, now);
                }

                if (v.NextMaintenanceOdometer.HasValue && v.OdometerCurrent.HasValue)
                {
                    var remainingKm = v.NextMaintenanceOdometer.Value - v.OdometerCurrent.Value;
                    if (remainingKm <= kmAhead)
                    {
                        EnqueueForAll(v, NotificationKind.VehicleMaintenanceDueSoon,
                            $"Плановое ТО {v.Make} {v.Model} ({v.LicensePlate}): осталось {remainingKm} км",
                            $"Плановое ТО ТС «{v.Make} {v.Model}» (гос. номер {v.LicensePlate}) при пробеге {v.NextMaintenanceOdometer} км. Текущий пробег: {v.OdometerCurrent} км. Осталось: {remainingKm} км.",
                            recipients, created, now);
                    }
                }
            }

            return created;
        }

        private void EnqueueForAll(Vehicle vehicle, NotificationKind kind,
                                   string title, string body,
                                   IReadOnlyList<Employee> recipients,
                                   List<Notification> created,
                                   DateTime now)
        {
            foreach (var r in recipients)
            {
                if (HasRecentNotificationForVehicle(r.Id, kind, vehicle.Id, now)) continue;

                // Передаём `now` как `createdAt`, чтобы записанная метка
                // времени совпадала с логическим временем сканирования —
                // дедуп по `n.CreatedAt.Date == now.Date` тогда корректен
                // и в проде, и в юнит-тестах с подменой часов.
                var n = _notifications.Create(r.Id, kind, title, body, createdAt: now);
                if (n != null) created.Add(n);
            }
        }

        /// <summary>
        /// Проверка идемпотентности: для одного и того же ТС за один логический
        /// календарный день одинаковое уведомление повторно не создаётся.
        /// «Сегодня» определяется параметром <paramref name="now"/>, чтобы
        /// каталог-обход и дедуп использовали единые часы — тесты могут
        /// подменять время, а в проде планировщик передаёт <c>DateTime.Now</c>.
        /// Идентификатор ТС зашит в <see cref="Notification.Body"/> через гос.
        /// номер — этого достаточно, т.к. одна и та же модель/гос.номер не
        /// пересекаются.
        /// </summary>
        private bool HasRecentNotificationForVehicle(int recipientId, NotificationKind kind,
                                                     int vehicleId, DateTime now)
        {
            var existing = _notificationRepo.ListByRecipient(recipientId, unreadOnly: false);
            return existing.Any(n => n.Kind == kind
                                     && n.CreatedAt.Date == now.Date
                                     && (n.Body ?? string.Empty).Contains($"гос. номер {GetVehicleNumber(vehicleId)}"));
        }

        private string GetVehicleNumber(int vehicleId)
        {
            var v = _vehicles.GetVehicle(vehicleId);
            return v?.LicensePlate ?? string.Empty;
        }
    }
}
