using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация <see cref="IMaintenanceRequestRepository"/>.</summary>
    public sealed class InMemoryMaintenanceRequestRepository : IMaintenanceRequestRepository
    {
        private readonly Dictionary<int, MaintenanceRequest> _store
            = new Dictionary<int, MaintenanceRequest>();
        private int _next = 1;

        public MaintenanceRequest Add(MaintenanceRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.BuildingId <= 0)
                throw new ArgumentException("Заявка должна быть привязана к зданию.", nameof(request));
            if (request.RequesterEmployeeId <= 0)
                throw new ArgumentException("Заявка должна иметь автора.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Описание заявки обязательно.", nameof(request));

            request.Id = _next++;
            _store[request.Id] = request;
            return request;
        }

        public MaintenanceRequest Get(int id) => _store.TryGetValue(id, out var r) ? r : null;

        public IReadOnlyList<MaintenanceRequest> List(DateTime? from, DateTime? to,
            int? buildingId, MaintenanceStatus? status)
        {
            IEnumerable<MaintenanceRequest> q = _store.Values;
            if (from.HasValue) q = q.Where(r => r.RegistrationDate >= from.Value);
            if (to.HasValue) q = q.Where(r => r.RegistrationDate <= to.Value);
            if (buildingId.HasValue) q = q.Where(r => r.BuildingId == buildingId.Value);
            if (status.HasValue) q = q.Where(r => r.Status == status.Value);

            return q.OrderBy(r => r.Status)
                    .ThenByDescending(r => r.Priority)
                    .ThenByDescending(r => r.RegistrationDate)
                    .ToList()
                    .AsReadOnly();
        }

        public IReadOnlyList<MaintenanceRequest> ListByAssignee(int employeeId)
            => _store.Values
                .Where(r => r.AssigneeEmployeeId == employeeId)
                .OrderBy(r => r.Status)
                .ThenByDescending(r => r.Priority)
                .ThenByDescending(r => r.RegistrationDate)
                .ToList()
                .AsReadOnly();

        public MaintenanceRequest Update(MaintenanceRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!_store.ContainsKey(request.Id))
                throw new InvalidOperationException("Заявка не найдена.");
            _store[request.Id] = request;
            return request;
        }

        public void Delete(int id) => _store.Remove(id);
    }
}
