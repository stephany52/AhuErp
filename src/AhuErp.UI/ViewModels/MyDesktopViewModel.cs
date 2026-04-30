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
