using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Абстракция хранилища автопарка. На Phase 4 используется in-memory реализация;
    /// при переходе на EF6 подменяется адаптером над <see cref="Data.AhuDbContext"/>
    /// без изменений в сервисе и UI.
    /// </summary>
    public interface IVehicleRepository
    {
        IReadOnlyList<Vehicle> ListVehicles();

        Vehicle GetVehicle(int vehicleId);

        /// <summary>Возвращает все поездки выбранного ТС в хронологическом порядке.</summary>
        IReadOnlyList<VehicleTrip> ListTrips(int vehicleId);

        void AddVehicle(Vehicle vehicle);

        void AddTrip(VehicleTrip trip);

        /// <summary>
        /// Возвращает поездку по идентификатору; <c>null</c>, если не найдена.
        /// Используется для отмены путевого листа из РКК.
        /// </summary>
        VehicleTrip GetTrip(int tripId);

        /// <summary>
        /// Физически удаляет поездку. Применяется при отмене ошибочно
        /// созданного путевого листа: запись бронирования снимается, чтобы
        /// освободить интервал, а действие фиксируется записью в журнале аудита.
        /// </summary>
        void RemoveTrip(int tripId);
    }
}
