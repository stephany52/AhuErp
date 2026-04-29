using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IVehicleRepository"/>. Изменения статуса ТС
    /// (<see cref="VehicleStatus.OnMission"/> при бронировании) на трекаемом
    /// <see cref="Vehicle"/> EF6 фиксирует автоматически — <see cref="AddTrip"/>
    /// одним <c>SaveChanges</c> сохраняет и новую поездку, и изменение статуса.
    /// </summary>
    public sealed class EfVehicleRepository : IVehicleRepository
    {
        private readonly AhuDbContext _ctx;

        public EfVehicleRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public IReadOnlyList<Vehicle> ListVehicles() =>
            _ctx.Vehicles.ToList().AsReadOnly();

        public Vehicle GetVehicle(int vehicleId) => _ctx.Vehicles.Find(vehicleId);

        public IReadOnlyList<VehicleTrip> ListTrips(int vehicleId) =>
            _ctx.VehicleTrips
                .Where(t => t.VehicleId == vehicleId)
                .OrderBy(t => t.StartDate)
                .ToList()
                .AsReadOnly();

        public void AddVehicle(Vehicle vehicle)
        {
            if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
            _ctx.Vehicles.Add(vehicle);
            _ctx.SaveChanges();
        }

        public void AddTrip(VehicleTrip trip)
        {
            if (trip == null) throw new ArgumentNullException(nameof(trip));
            _ctx.VehicleTrips.Add(trip);
            _ctx.SaveChanges();
        }

        public VehicleTrip GetTrip(int tripId) => _ctx.VehicleTrips.Find(tripId);

        public void RemoveTrip(int tripId)
        {
            var trip = _ctx.VehicleTrips.Find(tripId);
            if (trip == null) return;
            _ctx.VehicleTrips.Remove(trip);
            _ctx.SaveChanges();
        }
    }
}
