using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 9 — фасад над <see cref="INotificationRepository"/> и
    /// <see cref="IEmailGateway"/>. Учитывает индивидуальные предпочтения
    /// сотрудника (<see cref="NotificationPreference"/>) и обрабатывает
    /// дедлайны через <see cref="TickReminders"/>.
    /// </summary>
    public interface INotificationService
    {
        Notification Create(int recipientId, NotificationKind kind, string title,
                            string body, int? docId = null, int? taskId = null,
                            int? approvalId = null, DateTime? createdAt = null);

        void MarkRead(int notificationId, int actorId);
        void MarkAllRead(int recipientId);

        IReadOnlyList<Notification> ListForUser(int recipientId, bool unreadOnly = false);
        int CountUnread(int recipientId);

        /// <summary>
        /// Идемпотентный обход всех активных задач: создаёт
        /// <see cref="NotificationKind.TaskDeadlineSoon"/> за 24 часа до Deadline
        /// и <see cref="NotificationKind.TaskOverdue"/> при наступлении просрочки.
        /// Повторный вызов в рамках того же события записей не дублирует.
        /// </summary>
        void TickReminders(DateTime now);

        // Preferences (A11) -------------------------------------------------

        /// <summary>
        /// Все настройки уведомлений сотрудника. Если по типу нет записи —
        /// действует умолчание (InApp + IsEnabled = true).
        /// </summary>
        IReadOnlyList<NotificationPreference> ListPreferences(int employeeId);

        /// <summary>
        /// Сохранить настройку уведомления для сотрудника. Если запись по
        /// этому <see cref="NotificationKind"/> существует — обновится, иначе
        /// будет создана новая.
        /// </summary>
        void SetPreference(int employeeId, NotificationKind kind,
                           NotificationChannel channel, bool isEnabled,
                           string emailOverride = null);
    }
}
