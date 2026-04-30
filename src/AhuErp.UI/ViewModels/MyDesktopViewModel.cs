using System;
using System.Collections.ObjectModel;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using AhuErp.UI.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Phase 9 — «Мой рабочий стол». Сводка для текущего пользователя:
    /// поручения, согласования, резолюции и колонка in-app уведомлений.
    /// Учитывает активное замещение для пользователя (баннер в шапке).
    /// </summary>
    public partial class MyDesktopViewModel : ViewModelBase
    {
        private readonly IAuthService _auth;
        private readonly ITaskService _taskService;
        private readonly IApprovalService _approvalService;
        private readonly INotificationService _notifications;
        private readonly ISubstitutionService _substitutions;
        private readonly IDocumentNavigator _navigator;

        public ObservableCollection<DocumentTask> Tasks { get; } = new ObservableCollection<DocumentTask>();
        public ObservableCollection<DocumentApproval> Approvals { get; } = new ObservableCollection<DocumentApproval>();
        public ObservableCollection<Notification> Notifications { get; } = new ObservableCollection<Notification>();

        [ObservableProperty]
        private string greeting;

        [ObservableProperty]
        private int unreadCount;

        [ObservableProperty]
        private string substitutionBanner;

        public MyDesktopViewModel(
            IAuthService auth,
            ITaskService taskService,
            IApprovalService approvalService,
            INotificationService notifications,
            ISubstitutionService substitutions,
            IDocumentNavigator navigator = null)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _approvalService = approvalService ?? throw new ArgumentNullException(nameof(approvalService));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _substitutions = substitutions ?? throw new ArgumentNullException(nameof(substitutions));
            _navigator = navigator;

            Reload();
        }

        /// <summary>
        /// Открыть РКК документа, к которому привязана выбранная карточка
        /// (поручение или уведомление). Никаких действий, если кросс-VM
        /// навигатор не зарегистрирован или у объекта нет связи с документом.
        /// </summary>
        [RelayCommand]
        public void OpenDocument(object payload)
        {
            if (_navigator == null) return;
            int? docId = null;
            if (payload is DocumentTask task) docId = task.DocumentId;
            else if (payload is Notification n) docId = n.RelatedDocumentId;
            else if (payload is DocumentApproval a) docId = a.DocumentId;
            if (docId.HasValue && docId.Value > 0)
                _navigator.OpenDocument(docId.Value);
        }

        [RelayCommand]
        public void Reload()
        {
            var me = _auth.CurrentEmployee;
            if (me == null)
            {
                Greeting = null;
                return;
            }

            Greeting = $"Здравствуйте, {me.FullName}.";

            Tasks.Clear();
            foreach (var t in _taskService.ListMyTasks(me.Id, MyTasksScope.AsExecutor)
                                          .OrderBy(x => x.Deadline))
            {
                Tasks.Add(t);
            }

            Approvals.Clear();
            // Запрос согласований по этапам, ожидающим текущего пользователя:
            // упрощённо — сканируем последние 100 уведомлений ApprovalRequired
            // и подбираем уникальные approvalId.
            var pendingNotifs = _notifications.ListForUser(me.Id, unreadOnly: false)
                .Where(n => n.Kind == NotificationKind.ApprovalRequired && n.RelatedApprovalId.HasValue)
                .ToList();
            // Реальный сценарий — IApprovalService.ListPendingForApprover, но
            // он не входит в текущий API; используем уведомления как индекс.
            // Для UI этого достаточно; точная фильтрация попадёт в Phase 12.
            foreach (var notif in pendingNotifs)
            {
                // Заглушка — в текущей версии Approvals остаётся пустым,
                // т.к. без расширения IApprovalService получить полный объект
                // согласования по Id нельзя. Список уведомлений в правой
                // колонке покрывает функционал.
            }

            Notifications.Clear();
            foreach (var n in _notifications.ListForUser(me.Id, unreadOnly: false).Take(50))
            {
                Notifications.Add(n);
            }
            UnreadCount = _notifications.CountUnread(me.Id);

            var sub = _substitutions.GetActiveSubstitute(me.Id, DateTime.Now, SubstitutionScope.Full);
            SubstitutionBanner = sub != null
                ? $"Активно замещение до {sub.To:dd.MM.yyyy}: исполняет {sub.SubstituteEmployee?.FullName ?? "(заместитель)"}."
                : null;
        }

        [RelayCommand]
        public void MarkAllRead()
        {
            var me = _auth.CurrentEmployee;
            if (me == null) return;
            _notifications.MarkAllRead(me.Id);
            Reload();
        }

        [RelayCommand]
        public void MarkRead(Notification n)
        {
            if (n == null || _auth.CurrentEmployee == null) return;
            _notifications.MarkRead(n.Id, _auth.CurrentEmployee.Id);
            Reload();
        }
    }
}
