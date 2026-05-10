using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="IMaintenanceRequestRepository"/>.</summary>
    public sealed class EfMaintenanceRequestRepository : IMaintenanceRequestRepository
    {
        private readonly AhuDbContext _ctx;

        public EfMaintenanceRequestRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public MaintenanceRequest Add(MaintenanceRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.BuildingId <= 0)
                throw new ArgumentException("Заявка должна быть привязана к зданию.", nameof(request));
            if (request.RequesterEmployeeId <= 0)
                throw new ArgumentException("Заявка должна иметь автора.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Описание заявки обязательно.", nameof(request));

            _ctx.MaintenanceRequests.Add(request);
            _ctx.SaveChanges();
            return request;
        }

        public MaintenanceRequest Get(int id) => _ctx.MaintenanceRequests.Find(id);

        public IReadOnlyList<MaintenanceRequest> List(DateTime? from, DateTime? to,
            int? buildingId, MaintenanceStatus? status)
        {
            IQueryable<MaintenanceRequest> q = _ctx.MaintenanceRequests;
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
            => _ctx.MaintenanceRequests
                .Where(r => r.AssigneeEmployeeId == employeeId)
                .OrderBy(r => r.Status)
                .ThenByDescending(r => r.Priority)
                .ThenByDescending(r => r.RegistrationDate)
                .ToList()
                .AsReadOnly();

        public MaintenanceRequest Update(MaintenanceRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            _ctx.Entry(request).State = EntityState.Modified;
            _ctx.SaveChanges();
            return request;
        }

        public void Delete(int id)
        {
            var existing = _ctx.MaintenanceRequests.Find(id);
            if (existing == null) return;
            _ctx.MaintenanceRequests.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
