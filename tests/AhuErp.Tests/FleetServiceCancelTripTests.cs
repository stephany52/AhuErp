using System;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 12 / A6 — отмена путевого листа из РКК. Запись физически
    /// удаляется, статус ТС возвращается в Available, если активных
    /// бронирований не осталось.
    /// </summary>
    public class FleetServiceCancelTripTests
    {
        private readonly InMemoryVehicleRepository _repo = new InMemoryVehicleRepository();
        private readonly FleetService _service;

        public FleetServiceCancelTripTests()
        {
            _service = new FleetService(_repo);
        }

        private Vehicle SeedVehicle()
        {
            var v = new Vehicle
            {
                Model = "UAZ Patriot",
                LicensePlate = "А777АА 64",
                CurrentStatus = VehicleStatus.Available,
            };
            _repo.AddVehicle(v);
            return v;
        }

        [Fact]
        public void CancelTrip_removes_trip_and_returns_status_to_available()
        {
            var vehicle = SeedVehicle();
            var trip = _service.BookVehicle(vehicle.Id, documentId: 42,
                startDate: new DateTime(2026, 5, 1, 8, 0, 0),
                endDate: new DateTime(2026, 5, 1, 18, 0, 0),
                driverName: "Иванов И.И.");

            var cancelled = _service.CancelTrip(trip.Id, actorId: 1, reason: "Ошибочно создан");

            Assert.Equal(trip.Id, cancelled.Id);
            Assert.Null(_repo.GetTrip(trip.Id));
            // После отмены последнего активного бронирования статус возвращается.
            Assert.Equal(VehicleStatus.Available, vehicle.CurrentStatus);
        }

        [Fact]
        public void CancelTrip_throws_when_trip_not_found()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => _service.CancelTrip(tripId: 9999, actorId: 1));
            Assert.Contains("9999", ex.Message);
        }

        [Fact]
        public void CancelTrip_requires_actor_for_audit()
        {
            var vehicle = SeedVehicle();
            var trip = _service.BookVehicle(vehicle.Id, documentId: 42,
                startDate: new DateTime(2026, 5, 1, 8, 0, 0),
                endDate: new DateTime(2026, 5, 1, 18, 0, 0),
                driverName: "Иванов И.И.");

            Assert.Throws<ArgumentException>(
                () => _service.CancelTrip(trip.Id, actorId: 0));
        }

        [Fact]
        public void CancelTrip_requires_repository_overload()
        {
            // Конструктор без репозитория используется только в Phase 1-тестах
            // BookVehicle(Vehicle, …); CancelTrip без репозитория недоступна.
            var raw = new FleetService();
            Assert.Throws<InvalidOperationException>(
                () => raw.CancelTrip(tripId: 1, actorId: 1));
        }
    }
}
