using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IVehicleRepository"/> для демо/тестов.
    /// </summary>
    public sealed class InMemoryVehicleRepository : IVehicleRepository
    {
        private readonly List<Vehicle> _vehicles = new List<Vehicle>();
        private readonly List<VehicleTrip> _trips = new List<VehicleTrip>();
        private int _nextVehicleId = 1;
        private int _nextTripId = 1;

        public IReadOnlyList<Vehicle> ListVehicles() => _vehicles.ToList();

        public Vehicle GetVehicle(int vehicleId) =>
            _vehicles.FirstOrDefault(v => v.Id == vehicleId);

        public IReadOnlyList<VehicleTrip> ListTrips(int vehicleId) =>
            _trips.Where(t => t.VehicleId == vehicleId)
                  .OrderBy(t => t.StartDate)
                  .ToList();

        public void AddVehicle(Vehicle vehicle)
        {
            if (vehicle.Id == 0) vehicle.Id = _nextVehicleId++;
            else _nextVehicleId = System.Math.Max(_nextVehicleId, vehicle.Id + 1);
            _vehicles.Add(vehicle);
        }

        public void AddTrip(VehicleTrip trip)
        {
            if (trip.Id == 0) trip.Id = _nextTripId++;
            else _nextTripId = System.Math.Max(_nextTripId, trip.Id + 1);
            _trips.Add(trip);

            var vehicle = GetVehicle(trip.VehicleId);
            if (vehicle != null && !vehicle.Trips.Contains(trip))
            {
                vehicle.Trips.Add(trip);
            }
        }

        public VehicleTrip GetTrip(int tripId) =>
            _trips.FirstOrDefault(t => t.Id == tripId);

        public void RemoveTrip(int tripId)
        {
            var trip = _trips.FirstOrDefault(t => t.Id == tripId);
            if (trip == null) return;
            _trips.Remove(trip);
            var vehicle = GetVehicle(trip.VehicleId);
            vehicle?.Trips.Remove(trip);
        }
    }
}
