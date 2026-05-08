using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using AhuErp.UI.Messaging;
using AhuErp.UI.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Bug #2 — поведение <see cref="MyDesktopViewModel"/>: после нажатия
    /// «Прочитано» уведомление должно исчезать из ленты при включённом
    /// фильтре «Только непрочитанные», а счётчик в шапке — обновляться
    /// через <see cref="IMessenger"/>.
    /// </summary>
    public sealed class MyDesktopViewModelTests
    {
        private readonly InMemoryNotificationRepository _notifRepo = new InMemoryNotificationRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly InMemoryEmployeeRepository _employees;
        private readonly InMemoryTaskRepository _tasks = new InMemoryTaskRepository();
        private readonly InMemoryDocumentRepository _docs = new InMemoryDocumentRepository();
        private readonly InMemoryApprovalRepository _approvals = new InMemoryApprovalRepository();
        private readonly InMemorySubstitutionRepository _subs = new InMemorySubstitutionRepository();
        private readonly NoOpEmailGateway _email = new NoOpEmailGateway();
        private readonly AuditService _audit;
        private readonly NotificationService _notifications;
        private readonly TaskService _taskService;
        private readonly ApprovalService _approvalService;
        private readonly SubstitutionService _substitutions;
        private readonly StubAuthService _auth;

        public MyDesktopViewModelTests()
        {
            _employees = new InMemoryEmployeeRepository(new[]
            {
                new Employee { Id = 1, FullName = "Иванов И.И.", Email = "i@bmr", Role = EmployeeRole.Manager },
            });
            _audit = new AuditService(_auditRepo);
            _notifications = new NotificationService(_notifRepo, _employees, _tasks, _audit, _email);
            _taskService = new TaskService(_tasks, _docs, _audit);
            _approvalService = new ApprovalService(_approvals, _docs, _audit);
            _substitutions = new SubstitutionService(_subs, _audit);
            _auth = new StubAuthService(_employees.GetById(1));
        }

        private MyDesktopViewModel BuildVm(IMessenger messenger = null) =>
            new MyDesktopViewModel(_auth, _taskService, _approvalService,
                _notifications, _substitutions, navigator: null,
                messenger: messenger ?? new StrongReferenceMessenger());

        [Fact]
        public void MarkRead_RemovesFromCollection_WhenShowOnlyUnreadIsTrue()
        {
            _notifications.Create(1, NotificationKind.System, "A", "...");
            _notifications.Create(1, NotificationKind.System, "B", "...");

            var vm = BuildVm();
            Assert.True(vm.ShowOnlyUnread);
            Assert.Equal(2, vm.Notifications.Count);
            Assert.Equal(2, vm.UnreadCount);

            var first = vm.Notifications.First();
            vm.MarkReadCommand.Execute(first);

            Assert.DoesNotContain(first, vm.Notifications);
            Assert.Single(vm.Notifications);
            Assert.Equal(1, vm.UnreadCount);
            // Сервис тоже считает её прочитанной.
            Assert.Equal(1, _notifications.CountUnread(1));
        }

        [Fact]
        public void MarkRead_KeepsItem_WhenShowOnlyUnreadIsFalse_AndMarksReadInPlace()
        {
            _notifications.Create(1, NotificationKind.System, "A", "...");
            _notifications.Create(1, NotificationKind.System, "B", "...");

            var vm = BuildVm();
            vm.ShowOnlyUnread = false; // переключаем фильтр — Reload подхватит обе записи

            Assert.Equal(2, vm.Notifications.Count);
            var first = vm.Notifications.First();
            Assert.False(first.IsRead);

            vm.MarkReadCommand.Execute(first);

            // Карточка осталась в ленте, но помечена прочитанной — DataTrigger
            // в DataTemplate тушит её до Opacity 0.5 (см. MyDesktopView.xaml).
            Assert.Contains(first, vm.Notifications);
            Assert.True(first.IsRead);
            Assert.NotNull(first.ReadAt);
            Assert.Equal(1, vm.UnreadCount);
        }

        [Fact]
        public void MarkAllRead_ClearsCollection_WhenShowOnlyUnread()
        {
            _notifications.Create(1, NotificationKind.System, "A", "...");
            _notifications.Create(1, NotificationKind.System, "B", "...");

            var vm = BuildVm();
            Assert.Equal(2, vm.UnreadCount);

            vm.MarkAllReadCommand.Execute(null);

            Assert.Empty(vm.Notifications);
            Assert.Equal(0, vm.UnreadCount);
            Assert.Equal(0, _notifications.CountUnread(1));
        }

        [Fact]
        public void MarkAllRead_KeepsItems_WhenShowOnlyUnreadIsFalse_AndMarksAllInPlace()
        {
            _notifications.Create(1, NotificationKind.System, "A", "...");
            _notifications.Create(1, NotificationKind.System, "B", "...");

            var vm = BuildVm();
            vm.ShowOnlyUnread = false;

            vm.MarkAllReadCommand.Execute(null);

            Assert.Equal(2, vm.Notifications.Count);
            Assert.All(vm.Notifications, n => Assert.True(n.IsRead));
            Assert.Equal(0, vm.UnreadCount);
        }

        [Fact]
        public void MarkRead_PublishesUnreadCountChanged_OnMessenger()
        {
            _notifications.Create(1, NotificationKind.System, "A", "...");
            _notifications.Create(1, NotificationKind.System, "B", "...");

            var messenger = new StrongReferenceMessenger();
            var sink = new UnreadSink();
            messenger.Register<UnreadCountChangedMessage>(sink, (recipient, msg) =>
                ((UnreadSink)recipient).LastValue = msg.Value);

            var vm = BuildVm(messenger);
            var first = vm.Notifications.First();
            vm.MarkReadCommand.Execute(first);

            Assert.Equal(1, sink.LastValue);

            vm.MarkAllReadCommand.Execute(null);
            Assert.Equal(0, sink.LastValue);
        }

        [Fact]
        public void Reload_HonoursShowOnlyUnreadFilter()
        {
            _notifications.Create(1, NotificationKind.System, "A", "...");
            var second = _notifications.Create(1, NotificationKind.System, "B", "...");
            _notifications.MarkRead(second.Id, actorId: 1);

            var vm = BuildVm();
            // По умолчанию ShowOnlyUnread=true → одна непрочитанная.
            Assert.Single(vm.Notifications);

            vm.ShowOnlyUnread = false;
            Assert.Equal(2, vm.Notifications.Count);
        }

        private sealed class UnreadSink
        {
            public int LastValue { get; set; } = -1;
        }

        private sealed class StubAuthService : IAuthService
        {
            public StubAuthService(Employee employee)
            {
                CurrentEmployee = employee;
            }

            public Employee CurrentEmployee { get; private set; }
            public bool IsAuthenticated => CurrentEmployee != null;
            public LoginFailureReason LastFailureReason => LoginFailureReason.None;

            public bool TryLogin(string fullName, string password) => false;

            public void Logout() => CurrentEmployee = null;
        }
    }
}
