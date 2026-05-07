using System;
using System.Collections.ObjectModel;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using AhuErp.UI.Infrastructure;
using AhuErp.UI.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

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
        private readonly IMessenger _messenger;

        public ObservableCollection<DocumentTask> Tasks { get; } = new ObservableCollection<DocumentTask>();
        public ObservableCollection<DocumentApproval> Approvals { get; } = new ObservableCollection<DocumentApproval>();
        public ObservableCollection<DocumentResolution> Resolutions { get; } = new ObservableCollection<DocumentResolution>();
        public ObservableCollection<EmployeeDisciplineRow> DisciplineRows { get; } = new ObservableCollection<EmployeeDisciplineRow>();
        public ObservableCollection<Notification> Notifications { get; } = new ObservableCollection<Notification>();

        public ApprovalDecision[] ApprovalStatuses { get; } =
        {
            ApprovalDecision.Pending,
            ApprovalDecision.Approved,
            ApprovalDecision.Rejected
        };

        [ObservableProperty]
        private string greeting;

        [ObservableProperty]
        private int unreadCount;

        [ObservableProperty]
        private string substitutionBanner;

        [ObservableProperty]
        private ApprovalDecision selectedApprovalStatus = ApprovalDecision.Pending;

        [ObservableProperty]
        private string disciplineSummary;

        /// <summary>
        /// Bug #2 — фильтр ленты уведомлений. По умолчанию показываются только
        /// непрочитанные, чтобы карточка после нажатия «Прочитано» сразу
        /// исчезала из списка. Чекбокс «Показывать только непрочитанные» в
        /// заголовке колонки переключает состояние.
        /// </summary>
        [ObservableProperty]
        private bool showOnlyUnread = true;

        public MyDesktopViewModel(
            IAuthService auth,
            ITaskService taskService,
            IApprovalService approvalService,
            INotificationService notifications,
            ISubstitutionService substitutions,
            IDocumentNavigator navigator = null,
            IMessenger messenger = null)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _approvalService = approvalService ?? throw new ArgumentNullException(nameof(approvalService));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _substitutions = substitutions ?? throw new ArgumentNullException(nameof(substitutions));
            _navigator = navigator;
            _messenger = messenger ?? WeakReferenceMessenger.Default;

            Reload();
        }

        partial void OnShowOnlyUnreadChanged(bool value) => ReloadNotifications();

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
            else if (payload is DocumentResolution r) docId = r.DocumentId;
            if (docId.HasValue && docId.Value > 0)
                _navigator.OpenDocument(docId.Value);
        }

        partial void OnSelectedApprovalStatusChanged(ApprovalDecision value) => ReloadApprovals();

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

            ReloadApprovals();

            Resolutions.Clear();
            foreach (var r in _taskService.ListResolutionsByAuthor(me.Id)
                                          .OrderByDescending(x => x.IssuedAt))
            {
                Resolutions.Add(r);
            }

            var report = _taskService.BuildDisciplineReport(
                DateTime.Today.AddDays(-30),
                DateTime.Today.AddDays(1).AddSeconds(-1));
            DisciplineSummary =
                $"За 30 дней: всего {report.TotalTasks}, в срок {report.CompletedOnTime}, " +
                $"после срока {report.CompletedLate}, просрочено {report.Overdue}, " +
                $"исполнение {report.TimelyExecutionRate:P0}.";
            DisciplineRows.Clear();
            foreach (var row in report.ByExecutor ?? Array.Empty<EmployeeDisciplineRow>())
                DisciplineRows.Add(row);

            ReloadNotifications();

            var sub = _substitutions.GetActiveSubstitute(me.Id, DateTime.Now, SubstitutionScope.Full);
            SubstitutionBanner = sub != null
                ? $"Активно замещение до {sub.To:dd.MM.yyyy}: исполняет {sub.SubstituteEmployee?.FullName ?? "(заместитель)"}."
                : null;
        }

        private void ReloadNotifications()
        {
            var me = _auth.CurrentEmployee;
            Notifications.Clear();
            if (me == null)
            {
                UnreadCount = 0;
                return;
            }

            foreach (var n in _notifications.ListForUser(me.Id, unreadOnly: ShowOnlyUnread).Take(50))
            {
                Notifications.Add(n);
            }
            UnreadCount = _notifications.CountUnread(me.Id);
        }

        private void ReloadApprovals()
        {
            Approvals.Clear();
            var me = _auth.CurrentEmployee;
            if (me == null) return;
            foreach (var approval in _approvalService.ListForApprover(me.Id, SelectedApprovalStatus)
                                                     .OrderBy(a => a.Decision == ApprovalDecision.Pending ? 0 : 1)
                                                     .ThenBy(a => a.Document?.Deadline ?? DateTime.MaxValue)
                                                     .ThenByDescending(a => a.DecisionDate ?? DateTime.MinValue))
            {
                Approvals.Add(approval);
            }
        }

        [RelayCommand]
        public void MarkAllRead()
        {
            var me = _auth.CurrentEmployee;
            if (me == null) return;
            _notifications.MarkAllRead(me.Id);

            var now = DateTime.Now;
            if (ShowOnlyUnread)
            {
                Notifications.Clear();
            }
            else
            {
                // Notification — POCO без INotifyPropertyChanged: in-place
                // мутация ReadAt не поднимет DataTrigger по IsRead. Чтобы
                // карточки сразу затухли до Opacity 0.5, делаем Replace в
                // ObservableCollection — ItemsControl пересоздаст контейнеры
                // и DataTriggers перевычислятся по уже изменённому IsRead.
                for (int i = 0; i < Notifications.Count; i++)
                {
                    var item = Notifications[i];
                    if (!item.ReadAt.HasValue) item.ReadAt = now;
                    Notifications[i] = item;
                }
            }
            UnreadCount = _notifications.CountUnread(me.Id);
            _messenger.Send(new UnreadCountChangedMessage(UnreadCount));
        }

        [RelayCommand]
        public void MarkRead(Notification n)
        {
            var me = _auth.CurrentEmployee;
            if (n == null || me == null) return;
            _notifications.MarkRead(n.Id, me.Id);

            // Локальное состояние держим консистентным с сервисом.
            if (!n.ReadAt.HasValue) n.ReadAt = DateTime.Now;

            if (ShowOnlyUnread)
            {
                Notifications.Remove(n);
            }
            else
            {
                // POCO Notification без INotifyPropertyChanged — DataTrigger
                // по IsRead в DataTemplate не сработает на in-place мутации.
                // Replace по индексу провоцирует ItemsControl переcоздать
                // контейнер карточки с новой подсветкой.
                var idx = Notifications.IndexOf(n);
                if (idx >= 0) Notifications[idx] = n;
            }

            UnreadCount = _notifications.CountUnread(me.Id);
            _messenger.Send(new UnreadCountChangedMessage(UnreadCount));
        }
    }
}
