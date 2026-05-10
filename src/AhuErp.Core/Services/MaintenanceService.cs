using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IMaintenanceService"/>. Жизненный цикл заявки
    /// независим от <see cref="DocumentStatus"/>: заявка не требует
    /// согласования/подписания, поэтому используется простая стейт-машина
    /// <c>Open → InProgress → Completed | Cancelled</c>.
    /// </summary>
    public sealed class MaintenanceService : IMaintenanceService
    {
        private readonly IMaintenanceRequestRepository _repo;
        private readonly IAuditService _audit;
        private readonly INotificationService _notifications;

        public MaintenanceService(IMaintenanceRequestRepository repo, IAuditService audit,
            INotificationService notifications)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        }

        public MaintenanceRequest CreateRequest(MaintenanceRequest request, int actorId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.RegistrationDate == default)
                request.RegistrationDate = DateTime.Now;
            request.Status = MaintenanceStatus.Open;
            request.CompletedAt = null;
            request.Resolution = null;

            var saved = _repo.Add(request);
            _audit.Record(AuditActionType.MaintenanceRequestCreated,
                "MaintenanceRequest", saved.Id, actorId,
                details: $"Заявка #{saved.Id} зарегистрирована (вид: {saved.Kind}, приоритет: {saved.Priority}).");
            return saved;
        }

        public MaintenanceRequest Assign(int requestId, int assigneeEmployeeId, int actorId)
        {
            if (assigneeEmployeeId <= 0)
                throw new ArgumentException("Идентификатор исполнителя должен быть положительным.",
                    nameof(assigneeEmployeeId));

            var request = _repo.Get(requestId)
                ?? throw new InvalidOperationException($"Заявка #{requestId} не найдена.");

            if (request.Status == MaintenanceStatus.Completed
                || request.Status == MaintenanceStatus.Cancelled)
                throw new InvalidOperationException(
                    $"Заявка в терминальном статусе ({request.Status}) не может быть переназначена.");

            request.AssigneeEmployeeId = assigneeEmployeeId;
            if (request.Status == MaintenanceStatus.Open)
                request.Status = MaintenanceStatus.InProgress;

            var saved = _repo.Update(request);
            _audit.Record(AuditActionType.MaintenanceRequestAssigned,
                "MaintenanceRequest", saved.Id, actorId,
                details: $"Назначен исполнитель #{assigneeEmployeeId}.");
            _notifications.Create(assigneeEmployeeId, NotificationKind.TaskAssigned,
                "Назначена эксплуатационная заявка",
                $"Вам назначена заявка #{saved.Id} ({saved.Kind}, приоритет: {saved.Priority}).");
            return saved;
        }

        public MaintenanceRequest Complete(int requestId, string resolution, int actorId,
            DateTime? now = null)
        {
            if (string.IsNullOrWhiteSpace(resolution))
                throw new ArgumentException("Описание выполненных работ обязательно.", nameof(resolution));

            var request = _repo.Get(requestId)
                ?? throw new InvalidOperationException($"Заявка #{requestId} не найдена.");

            if (request.Status == MaintenanceStatus.Completed
                || request.Status == MaintenanceStatus.Cancelled)
                throw new InvalidOperationException(
                    $"Заявка уже находится в терминальном статусе ({request.Status}).");

            request.Status = MaintenanceStatus.Completed;
            request.Resolution = resolution;
            request.CompletedAt = now ?? DateTime.Now;

            var saved = _repo.Update(request);
            _audit.Record(AuditActionType.MaintenanceRequestStatusChanged,
                "MaintenanceRequest", saved.Id, actorId,
                details: "Заявка завершена.");
            return saved;
        }

        public MaintenanceRequest Cancel(int requestId, string reason, int actorId,
            DateTime? now = null)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина отмены обязательна.", nameof(reason));

            var request = _repo.Get(requestId)
                ?? throw new InvalidOperationException($"Заявка #{requestId} не найдена.");

            if (request.Status == MaintenanceStatus.Completed
                || request.Status == MaintenanceStatus.Cancelled)
                throw new InvalidOperationException(
                    $"Заявка уже находится в терминальном статусе ({request.Status}).");

            request.Status = MaintenanceStatus.Cancelled;
            request.Resolution = reason;
            request.CompletedAt = now ?? DateTime.Now;

            var saved = _repo.Update(request);
            _audit.Record(AuditActionType.MaintenanceRequestStatusChanged,
                "MaintenanceRequest", saved.Id, actorId,
                details: $"Заявка отменена: {reason}");
            return saved;
        }

        public IReadOnlyList<MaintenanceRequest> ListRequests(DateTime? from, DateTime? to,
            int? buildingId, MaintenanceStatus? status)
            => _repo.List(from, to, buildingId, status);
    }
}
