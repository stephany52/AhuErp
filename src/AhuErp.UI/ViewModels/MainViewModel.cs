using System;
using System.Collections.ObjectModel;
using System.Linq;
using AhuErp.Core.Services;
using AhuErp.UI.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
                             INotificationService notifications)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));

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
