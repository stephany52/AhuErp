using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="INotificationService"/>. Учитывает
    /// <see cref="NotificationPreference"/> для каждого получателя:
    /// если <see cref="NotificationPreference.IsEnabled"/> = false — запись не
    /// создаётся вовсе. В остальных случаях запись Notification ВСЕГДА
    /// сохраняется — её <see cref="Notification.Channel"/> фиксирует, как
    /// именно сообщение было/будет доставлено:
    /// <list type="bullet">
    ///   <item><description><see cref="NotificationChannel.InApp"/> —
    ///     показывается в ленте «Моего рабочего стола», письмо не
    ///     отправляется.</description></item>
    ///   <item><description><see cref="NotificationChannel.Email"/> —
    ///     отправляется e-mail; запись помечается как уже прочитанная,
    ///     поэтому не висит в счётчике (нужна для дедупликации
    ///     <see cref="TickReminders"/>).</description></item>
    ///   <item><description><see cref="NotificationChannel.Both"/> —
    ///     in-app + e-mail.</description></item>
    /// </list>
    /// Запись нужна также для идемпотентности
    /// <see cref="TickReminders"/>: проверка
    /// <see cref="INotificationRepository.ListByRelatedTask"/> работает
    /// независимо от выбранного канала.
    /// </summary>
    public sealed class NotificationService : INotificationService
    {
        private readonly INotificationRepository _repo;
        private readonly IEmployeeRepository _employees;
        private readonly ITaskRepository _tasks;
        private readonly IAuditService _audit;
        private readonly IEmailGateway _email;

        public NotificationService(
            INotificationRepository repo,
            IEmployeeRepository employees,
            ITaskRepository tasks,
            IAuditService audit,
            IEmailGateway email = null)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _employees = employees ?? throw new ArgumentNullException(nameof(employees));
            _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _email = email;
        }

        public Notification Create(int recipientId, NotificationKind kind, string title,
                                   string body, int? docId = null, int? taskId = null,
                                   int? approvalId = null, DateTime? createdAt = null)
        {
            if (recipientId <= 0) throw new ArgumentException("Получатель обязателен.", nameof(recipientId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Заголовок обязателен.", nameof(title));

            var pref = _repo.GetPreference(recipientId, kind);
            if (pref != null && !pref.IsEnabled)
            {
                // Сотрудник явно отписан от этого вида уведомлений.
                return null;
            }

            var channel = pref?.Channel ?? NotificationChannel.InApp;
            // CreatedAt: если вызывающий передал логическое время (например,
            // сканер автопарка с тестовым `now`), используем его — это
            // выравнивает дедуп по календарным дням между собственной логикой
            // вызывающего сервиса и записанной меткой времени уведомления.
            var now = createdAt ?? DateTime.Now;

            // Сохраняем запись ВСЕГДА — это нужно и для аудита, и для
            // идемпотентности TickReminders. Email-only записи помечаем как
            // прочитанные, чтобы не светиться в счётчике непрочитанных.
            var stored = _repo.Add(new Notification
            {
                RecipientId = recipientId,
                Kind = kind,
                Title = title,
                Body = body,
                RelatedDocumentId = docId,
                RelatedTaskId = taskId,
                RelatedApprovalId = approvalId,
                CreatedAt = now,
                ReadAt = channel == NotificationChannel.Email ? (DateTime?)now : null,
                Channel = channel,
            });
            _audit.Record(AuditActionType.NotificationSent, nameof(Notification),
                stored.Id, recipientId,
                newValues: $"Kind={kind}; Channel={channel}");

            // E-mail отправляем при Email/Both, если есть адрес и шлюз.
            if ((channel == NotificationChannel.Email || channel == NotificationChannel.Both)
                && _email != null)
            {
                var addr = !string.IsNullOrWhiteSpace(pref?.EmailOverride)
                    ? pref.EmailOverride
                    : _employees.GetById(recipientId)?.Email;
                if (!string.IsNullOrWhiteSpace(addr))
                {
                    try
                    {
                        _email.Send(addr, title, body);
                        stored.SentToEmailAt = DateTime.Now;
                        _repo.Update(stored);
                    }
                    catch
                    {
                        // SMTP сбой не должен валить бизнес-операцию.
                    }
                }
            }

            return channel == NotificationChannel.Email ? null : stored;
        }

        public void MarkRead(int notificationId, int actorId)
        {
            var n = _repo.Get(notificationId);
            if (n == null || n.ReadAt.HasValue) return;
            n.ReadAt = DateTime.Now;
            _repo.Update(n);
        }

        public void MarkAllRead(int recipientId)
        {
            foreach (var n in _repo.ListByRecipient(recipientId, unreadOnly: true))
            {
                n.ReadAt = DateTime.Now;
                _repo.Update(n);
            }
        }

        public IReadOnlyList<Notification> ListForUser(int recipientId, bool unreadOnly = false)
            => _repo.ListByRecipient(recipientId, unreadOnly);

        public int CountUnread(int recipientId) => _repo.CountUnread(recipientId);

        public void TickReminders(DateTime now)
        {
            // Просто проходим по всем активным задачам. Кол-во записей в
            // ERP-инсталляции МКУ ожидается на порядок 10^3, что укладывается
            // в одиночный проход.
            foreach (var t in _tasks.ListAll())
            {
                if (t.Status == DocumentTaskStatus.Completed
                    || t.Status == DocumentTaskStatus.Cancelled) continue;

                // 24 часа до дедлайна → DeadlineSoon (один раз ДЛЯ ТЕКУЩЕГО исполнителя).
                // После делегирования task.ExecutorId меняется — у нового сотрудника
                // напоминания ещё нет, поэтому проверяем per-recipient, а не per-task.
                if (now < t.Deadline && (t.Deadline - now).TotalHours <= 24
                    && _repo.ListByRelatedTaskAndRecipient(
                        t.Id, NotificationKind.TaskDeadlineSoon, t.ExecutorId).Count == 0)
                {
                    Create(t.ExecutorId, NotificationKind.TaskDeadlineSoon,
                        $"Срок поручения скоро истекает (#{t.Id})",
                        BuildBody(t),
                        docId: t.DocumentId, taskId: t.Id);
                }

                // Просрочено → Overdue (один раз ДЛЯ ТЕКУЩЕГО исполнителя).
                if (now > t.Deadline
                    && _repo.ListByRelatedTaskAndRecipient(
                        t.Id, NotificationKind.TaskOverdue, t.ExecutorId).Count == 0)
                {
                    Create(t.ExecutorId, NotificationKind.TaskOverdue,
                        $"Поручение просрочено (#{t.Id})",
                        BuildBody(t),
                        docId: t.DocumentId, taskId: t.Id);
                }
            }
        }

        private static string BuildBody(DocumentTask t)
            => $"Документ #{t.DocumentId}. Срок: {t.Deadline:dd.MM.yyyy HH:mm}. {t.Description}";

        public IReadOnlyList<NotificationPreference> ListPreferences(int employeeId)
            => _repo.ListPreferences(employeeId);

        public void SetPreference(int employeeId, NotificationKind kind,
                                  NotificationChannel channel, bool isEnabled,
                                  string emailOverride = null)
        {
            if (employeeId <= 0)
                throw new ArgumentException("employeeId обязателен.", nameof(employeeId));

            var existing = _repo.GetPreference(employeeId, kind);
            if (existing == null)
            {
                _repo.SetPreference(new NotificationPreference
                {
                    EmployeeId = employeeId,
                    Kind = kind,
                    Channel = channel,
                    IsEnabled = isEnabled,
                    EmailOverride = string.IsNullOrWhiteSpace(emailOverride) ? null : emailOverride.Trim()
                });
                return;
            }

            existing.Channel = channel;
            existing.IsEnabled = isEnabled;
            existing.EmailOverride = string.IsNullOrWhiteSpace(emailOverride) ? null : emailOverride.Trim();
            _repo.SetPreference(existing);
        }
    }
}
