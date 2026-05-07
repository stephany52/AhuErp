using System;
using System.Collections.ObjectModel;
using System.Linq;
using AhuErp.Core.Services;
using AhuErp.UI.Converters;
using AhuErp.UI.Infrastructure;
using AhuErp.UI.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Корневая ViewModel. Содержит список пунктов навигации, фильтрует их
    /// по роли текущего пользователя (<see cref="RolePolicy"/>) и управляет
    /// активной подстраницей <see cref="CurrentViewModel"/>.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        private readonly IAuthService _auth;
        private readonly RkkViewModel _rkkVm;

        public ObservableCollection<NavigationItem> NavigationItems { get; }

        [ObservableProperty]
        private NavigationItem selectedNavigationItem;

        [ObservableProperty]
        private ViewModelBase currentViewModel;

        [ObservableProperty]
        private string currentUserDisplayName;

        [ObservableProperty]
        private string currentUserRoleDisplayName;

        private readonly INotificationService _notifications;

        [ObservableProperty]
        private int unreadNotifications;

        public MainViewModel(IAuthService auth,
                             DashboardViewModel dashboardVm,
                             OfficeViewModel officeVm,
                             RkkViewModel rkkVm,
                             ArchiveViewModel archiveVm,
                             ItServiceViewModel itServiceVm,
                             FleetViewModel fleetVm,
                             WarehouseViewModel warehouseVm,
                             MyTasksViewModel myTasksVm,
                             NomenclatureViewModel nomenclatureVm,
                             AuditJournalViewModel auditJournalVm,
                             JournalViewModel journalVm,
                             SearchViewModel searchVm,
                             ReportsViewModel reportsVm,
                             OrgStructureViewModel orgStructureVm,
                             SubstitutionsViewModel substitutionsVm,
                             MyDesktopViewModel myDesktopVm,
                             NotificationPrefsViewModel notificationPrefsVm,
                             INotificationService notifications,
                             IDocumentNavigator navigator = null,
                             IMessenger messenger = null)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _rkkVm = rkkVm;

            // Регистрируемся в навигаторе документов, чтобы карточки и
            // уведомления на «Моём рабочем столе» могли открывать РКК.
            (navigator as DocumentNavigator)?.AttachMain(this);

            // Bug #2 — слушаем шину сообщений: MyDesktopViewModel.MarkRead
            // публикует UnreadCountChangedMessage, мы обновляем бейдж в шапке
            // без прямой ссылки между VM-ами.
            (messenger ?? WeakReferenceMessenger.Default).Register<UnreadCountChangedMessage>(
                this, (recipient, msg) => UnreadNotifications = msg.Value);

            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new NavigationItem("Мой рабочий стол", RolePolicy.MyDesktop, myDesktopVm),
                new NavigationItem("Дашборд",    RolePolicy.Dashboard, dashboardVm),
                new NavigationItem("РКК (документы)", RolePolicy.Office, rkkVm),
                new NavigationItem("Документационное обеспечение", RolePolicy.Office,    officeVm),
                new NavigationItem("Мои задачи",  RolePolicy.MyTasks,   myTasksVm),
                new NavigationItem("Архивный отдел", RolePolicy.Archive, archiveVm),
                new NavigationItem("Склад / ТМЦ", RolePolicy.Warehouse, warehouseVm),
                new NavigationItem("ИТО",        RolePolicy.ItService, itServiceVm),
                new NavigationItem("Транспорт",  RolePolicy.Fleet,     fleetVm),
                new NavigationItem("Номенклатура дел", RolePolicy.Nomenclature, nomenclatureVm),
                new NavigationItem("Журналы регистрации", RolePolicy.Journals, journalVm),
                new NavigationItem("Поиск", RolePolicy.Search, searchVm),
                new NavigationItem("Отчёты", RolePolicy.Reports, reportsVm),
                new NavigationItem("Оргструктура", RolePolicy.OrgStructure, orgStructureVm),
                new NavigationItem("Замещения", RolePolicy.Substitutions, substitutionsVm),
                new NavigationItem("Уведомления (настройки)", RolePolicy.NotificationPrefs, notificationPrefsVm),
                new NavigationItem("Журнал аудита", RolePolicy.AuditJournal, auditJournalVm),
            };

            ApplyRolePolicy();

            // Выбираем первый доступный пункт.
            foreach (var item in NavigationItems)
            {
                if (item.IsAllowed)
                {
                    SelectedNavigationItem = item;
                    break;
                }
            }
        }

        partial void OnSelectedNavigationItemChanged(NavigationItem value)
        {
            CurrentViewModel = value?.ViewModel;
        }

        [RelayCommand]
        private void NavigateTo(NavigationItem item)
        {
            if (item != null && item.IsAllowed) SelectedNavigationItem = item;
        }

        /// <summary>
        /// Phase 9 / A9 — переключиться на вкладку РКК и выбрать документ
        /// по идентификатору. Вызывается из «Моего рабочего стола» по
        /// двойному клику на карточке/уведомлении.
        /// </summary>
        public void NavigateToDocument(int documentId)
        {
            var item = NavigationItems.FirstOrDefault(n =>
                n.IsAllowed && n.ViewModel is RkkViewModel);
            if (item == null) return;

            SelectedNavigationItem = item;

            if (_rkkVm == null) return;
            if (_rkkVm.ReloadCommand.CanExecute(null))
                _rkkVm.ReloadCommand.Execute(null);
            var doc = _rkkVm.Documents.FirstOrDefault(d => d.Id == documentId);
            if (doc != null) _rkkVm.SelectedDocument = doc;
        }

        /// <summary>
        /// Phase 9 — обновить счётчик непрочитанных в шапке. Дёргается
        /// DispatcherTimer-ом из <c>App.xaml.cs</c> + при ручной навигации.
        /// </summary>
        public void RefreshUnreadCount()
        {
            var me = _auth.CurrentEmployee;
            UnreadNotifications = me == null ? 0 : _notifications.CountUnread(me.Id);
        }

        [RelayCommand]
        private void OpenMyDesktop()
        {
            var item = NavigationItems.FirstOrDefault(n =>
                n.IsAllowed && n.ViewModel is MyDesktopViewModel);
            if (item != null) SelectedNavigationItem = item;

            // Принудительно перечитываем уведомления (счётчик в шапке + список
            // на рабочем столе), чтобы открытие бейджем работало как «прочитать
            // все непрочитанные».
            if (item?.ViewModel is MyDesktopViewModel desk)
            {
                desk.Reload();
            }
            RefreshUnreadCount();
        }

        [RelayCommand]
        private void Logout()
        {
            _auth.Logout();

            // Закрываем главное окно и просим App перезапустить цикл
            // login → main без выгрузки приложения. Полный Application.Shutdown
            // выполняется только если пользователь не пройдёт повторный вход.
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.Tag = "logout";
                mainWindow.Close();
            }
        }

        private void ApplyRolePolicy()
        {
            var employee = _auth.CurrentEmployee;
            if (employee == null)
            {
                foreach (var item in NavigationItems) item.IsAllowed = false;
                CurrentUserDisplayName = null;
                CurrentUserRoleDisplayName = null;
                return;
            }

            foreach (var item in NavigationItems)
            {
                item.IsAllowed = RolePolicy.IsAllowed(employee.Role, item.ModuleKey);
            }

            CurrentUserDisplayName = employee.FullName;
            CurrentUserRoleDisplayName = EnumDisplayConverter.Translate(employee.Role);

            RefreshUnreadCount();
        }
    }
}
