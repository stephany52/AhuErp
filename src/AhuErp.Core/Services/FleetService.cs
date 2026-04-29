using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IFleetService"/>. Функциональная перегрузка остаётся
    /// чистой (для Phase 1-тестов), а Phase 4-перегрузка работает через
    /// <see cref="IVehicleRepository"/>, который в бою подменяется EF6-адаптером.
    /// </summary>
    public class FleetService : IFleetService
    {
        private readonly IVehicleRepository _repository;

        public FleetService()
        {
        }

        public FleetService(IVehicleRepository repository)
        {
            _repository = repository;
        }

        public VehicleTrip BookVehicle(int vehicleId, int documentId, DateTime startDate, DateTime endDate, string driverName)
        {
            if (_repository == null)
            {
                throw new InvalidOperationException(
                    "Для этой перегрузки требуется IVehicleRepository. Используйте конструктор FleetService(IVehicleRepository).");
            }
            if (documentId <= 0)
            {
                throw new ArgumentException("Документ-основание обязателен для заявки на транспорт.", nameof(documentId));
            }
            if (string.IsNullOrWhiteSpace(driverName))
            {
                throw new ArgumentException("ФИО водителя обязательно.", nameof(driverName));
            }

            var vehicle = _repository.GetVehicle(vehicleId)
                ?? throw new VehicleBookingException($"Транспортное средство #{vehicleId} не найдено.");

            var existingTrips = _repository.ListTrips(vehicleId);
            var trip = BookVehicle(vehicle, startDate, endDate, existingTrips, documentId);
            trip.DriverName = driverName;

            _repository.AddTrip(trip);
            return trip;
        }

        public VehicleTrip BookVehicle(Vehicle vehicle, DateTime startDate, DateTime endDate,
            IEnumerable<VehicleTrip> existingTrips = null, int? documentId = null)
        {
            if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
            if (endDate <= startDate)
            {
                throw new VehicleBookingException(
                    "Дата окончания поездки должна быть строго позже даты начала.");
            }

            if (vehicle.CurrentStatus == VehicleStatus.Maintenance)
            {
                throw new VehicleBookingException(
                    $"Транспортное средство '{vehicle.LicensePlate}' находится на техническом обслуживании.");
            }

            if (existingTrips != null)
            {
                foreach (var trip in existingTrips)
                {
                    if (trip.VehicleId != vehicle.Id) continue;
                    if (trip.OverlapsWith(startDate, endDate))
                    {
                        throw new VehicleBookingException(
                            $"Транспортное средство '{vehicle.LicensePlate}' уже забронировано " +
                            $"с {trip.StartDate:yyyy-MM-dd HH:mm} по {trip.EndDate:yyyy-MM-dd HH:mm}.");
                    }
                }
            }

            var newTrip = new VehicleTrip
            {
                VehicleId = vehicle.Id,
                Vehicle = vehicle,
                StartDate = startDate,
                EndDate = endDate,
                DocumentId = documentId,
                BasisDocumentId = documentId
            };

            vehicle.Trips.Add(newTrip);
            vehicle.CurrentStatus = VehicleStatus.OnMission;
            return newTrip;
        }

        public VehicleTrip CancelTrip(int tripId, int actorId, string reason = null)
        {
            if (_repository == null)
            {
                throw new InvalidOperationException(
                    "Для этой перегрузки требуется IVehicleRepository. Используйте конструктор FleetService(IVehicleRepository).");
            }
            if (actorId <= 0)
            {
                throw new ArgumentException("actorId обязателен для аудита отмены.", nameof(actorId));
            }

            var trip = _repository.GetTrip(tripId)
                ?? throw new InvalidOperationException($"Путевой лист #{tripId} не найден.");

            _repository.RemoveTrip(tripId);

            // Если у ТС не осталось активных (пересекающихся с «сейчас») поездок,
            // возвращаем статус Available. Это соответствует поведению BookVehicle,
            // которое выставляет OnMission при создании.
            var vehicle = _repository.GetVehicle(trip.VehicleId);
            if (vehicle != null && vehicle.CurrentStatus == VehicleStatus.OnMission)
            {
                var now = DateTime.Now;
                var stillActive = false;
                foreach (var other in _repository.ListTrips(vehicle.Id))
                {
                    if (other.StartDate <= now && other.EndDate >= now)
                    {
                        stillActive = true;
                        break;
                    }
                }
                if (!stillActive) vehicle.CurrentStatus = VehicleStatus.Available;
            }

            return trip;
        }
    }
}
