using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using AhuErp.UI.ViewModels;

namespace AhuErp.UI.Converters
{
    /// <summary>
    /// Двусторонний конвертер enum ↔ русская подпись. Ровно одно место,
    /// где описаны переводы, чтобы не плодить дубликаты по XAML/VM. Реальные
    /// значения в БД и в коде остаются английскими (привязаны к маппингу EF6
    /// и к тестам), пользователь видит локализованные строки.
    /// </summary>
    public sealed class EnumDisplayConverter : IValueConverter
    {
        private static readonly IReadOnlyDictionary<Enum, string> Map = new Dictionary<Enum, string>
        {
            [VehicleStatus.Available] = "Доступен",
            [VehicleStatus.OnMission] = "В рейсе",
            [VehicleStatus.Maintenance] = "На обслуживании",

            [DocumentStatus.New] = "Новый",
            [DocumentStatus.InProgress] = "В работе",
            [DocumentStatus.OnHold] = "Приостановлен",
            [DocumentStatus.Completed] = "Завершён",
            [DocumentStatus.Cancelled] = "Отменён",

            [DocumentType.General] = "Общий",
            [DocumentType.Office] = "Документационное обеспечение",
            [DocumentType.Archive] = "Архивный отдел",
            [DocumentType.It] = "ИТО",
            [DocumentType.Fleet] = "Транспорт",
            [DocumentType.Incoming] = "Входящий",
            [DocumentType.Internal] = "Внутренний",
            [DocumentType.ArchiveRequest] = "Архивный запрос",

            [DocumentDirection.Internal] = "Внутренний",
            [DocumentDirection.Incoming] = "Входящий",
            [DocumentDirection.Outgoing] = "Исходящий",
            [DocumentDirection.Directive] = "Распорядительный",

            [DocumentAccessLevel.Public] = "Без ограничений",
            [DocumentAccessLevel.Internal] = "Для служебного пользования",
            [DocumentAccessLevel.Confidential] = "Конфиденциально",

            [DocumentTaskStatus.New] = "Новое",
            [DocumentTaskStatus.InProgress] = "В работе",
            [DocumentTaskStatus.OnReview] = "На проверке",
            [DocumentTaskStatus.Completed] = "Выполнено",
            [DocumentTaskStatus.Cancelled] = "Отменено",
            [DocumentTaskStatus.Overdue] = "Просрочено",

            [ApprovalDecision.Pending] = "Ожидает",
            [ApprovalDecision.Approved] = "Согласовано",
            [ApprovalDecision.Rejected] = "Отклонено",
            [ApprovalDecision.Comments] = "С замечаниями",

            [MyTasksScope.AsExecutor] = "Как исполнитель",
            [MyTasksScope.AsController] = "Как контролёр",
            [MyTasksScope.AsAuthor] = "Как автор",
            [MyTasksScope.Any] = "Любая",

            [JournalKind.Incoming] = "Входящие",
            [JournalKind.Outgoing] = "Исходящие",
            [JournalKind.Internal] = "Внутренние",
            [JournalKind.ByCase] = "По делу",
            [JournalKind.All] = "Все",

            [ApprovalRouteStatus.Draft] = "Черновик",
            [ApprovalRouteStatus.InProgress] = "В работе",
            [ApprovalRouteStatus.Completed] = "Согласовано",
            [ApprovalRouteStatus.Rejected] = "Отклонено",
            [ApprovalRouteStatus.Cancelled] = "Отменено",

            [SignatureKind.Simple] = "ПЭП (простая)",
            [SignatureKind.Enhanced] = "НЭП (усиленная)",
            [SignatureKind.Qualified] = "КЭП (квалифицированная)",

            [NotificationKind.TaskAssigned] = "Назначено поручение",
            [NotificationKind.TaskDeadlineSoon] = "Скоро срок поручения",
            [NotificationKind.TaskOverdue] = "Поручение просрочено",
            [NotificationKind.ApprovalRequired] = "Требуется согласование",
            [NotificationKind.ApprovalDecided] = "Решение по согласованию",
            [NotificationKind.ResolutionAdded] = "Добавлена резолюция",
            [NotificationKind.DocumentRegistered] = "Документ зарегистрирован",
            [NotificationKind.DocumentSigned] = "Документ подписан",
            [NotificationKind.System] = "Системное уведомление",

            [NotificationChannel.InApp] = "В системе",
            [NotificationChannel.Email] = "E-mail",
            [NotificationChannel.Both] = "В системе и e-mail",

            [SubstitutionScope.TasksOnly] = "Только поручения",
            [SubstitutionScope.ApprovalsOnly] = "Только согласования",
            [SubstitutionScope.Full] = "Поручения и согласования",

            [AttachmentKind.Draft] = "Проект",
            [AttachmentKind.Scan] = "Скан-копия",
            [AttachmentKind.Signed] = "Подписанный экземпляр",
            [AttachmentKind.Other] = "Прочее",

            [ArchiveRequestKind.SocialLegal] = "Социально-правовой запрос",
            [ArchiveRequestKind.Thematic] = "Тематический запрос",
            [ArchiveRequestKind.MunicipalLegalActCopy] = "Копия муниципального правового акта",
            [ArchiveRequestKind.PaidThematic] = "Платный тематический запрос",

            [InventoryCategory.Stationery] = "Канцелярские товары и бланки",
            [InventoryCategory.IT_Equipment] = "Оргтехника, расходные материалы и связь",
            [InventoryCategory.Cleaning_Supplies] = "Хозяйственные и эксплуатационные материалы",

            [EmployeeRole.Admin] = "Администратор",
            [EmployeeRole.Manager] = "Руководитель / начальник службы",
            [EmployeeRole.Archivist] = "Сотрудник архивного отдела",
            [EmployeeRole.TechSupport] = "Специалист ИТО",
            [EmployeeRole.WarehouseManager] = "Ответственный за ТМЦ",

            [AuditActionType.Created] = "Создание",
            [AuditActionType.Updated] = "Изменение",
            [AuditActionType.Deleted] = "Удаление",
            [AuditActionType.StatusChanged] = "Смена статуса",
            [AuditActionType.Registered] = "Регистрация",
            [AuditActionType.AssignedToCase] = "Прикреплено к делу",
            [AuditActionType.AttachmentAdded] = "Добавлено вложение",
            [AuditActionType.AttachmentVersioned] = "Новая версия вложения",
            [AuditActionType.AttachmentRemoved] = "Удалено вложение",
            [AuditActionType.AttachmentViewed] = "Просмотр вложения",
            [AuditActionType.ResolutionIssued] = "Резолюция",
            [AuditActionType.TaskAssigned] = "Назначено поручение",
            [AuditActionType.TaskCompleted] = "Поручение выполнено",
            [AuditActionType.TaskOverdue] = "Поручение просрочено",
            [AuditActionType.TaskReassigned] = "Поручение переназначено",
            [AuditActionType.ApprovalSent] = "Маршрут согласования запущен",
            [AuditActionType.ApprovalSigned] = "Согласовано",
            [AuditActionType.ApprovalRejected] = "Отклонено",
            [AuditActionType.InventoryTransactionRecorded] = "Движение ТМЦ",
            [AuditActionType.VehicleTripBooked] = "Путевой лист оформлен",
            [AuditActionType.ArchiveRequestProcessed] = "Архивный запрос обработан",
            [AuditActionType.ItTicketResolved] = "ИТ-заявка закрыта",
            [AuditActionType.SignatureAdded] = "Подписание",
            [AuditActionType.SignatureRevoked] = "Отзыв подписи",
            [AuditActionType.DocumentLocked] = "Документ заблокирован КЭП",
            [AuditActionType.NotificationSent] = "Уведомление отправлено",
            [AuditActionType.SubstitutionCreated] = "Замещение создано",
            [AuditActionType.SubstitutionCancelled] = "Замещение отменено",
            [AuditActionType.TaskDelegated] = "Поручение делегировано",
            [AuditActionType.DepartmentHeadAssigned] = "Назначен руководитель отдела",
            [AuditActionType.IndexRebuilt] = "Поисковый индекс перестроен",
            [AuditActionType.ReportGenerated] = "Отчёт сформирован",
            [AuditActionType.UserLogin] = "Вход в систему",
            [AuditActionType.UserLogout] = "Выход из системы",
            [AuditActionType.Other] = "Прочее",
        };

        public static string Translate(Enum value) =>
            value != null && Map.TryGetValue(value, out var label) ? label : value?.ToString();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum e) return Translate(e);
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Обратное преобразование не используется (ComboBox привязывает SelectedItem
            // к самому enum, а не к строке) — оставляем заглушку.
            throw new NotSupportedException();
        }
    }
}
