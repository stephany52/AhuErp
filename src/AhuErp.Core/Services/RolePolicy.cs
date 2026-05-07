using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Декларативное сопоставление <see cref="EmployeeRole"/> и разрешённых
    /// модулей навигации. Используется и ViewModel-ем главного окна для
    /// фильтрации меню, и (в будущем) серверной авторизацией команд.
    /// </summary>
    public static class RolePolicy
    {
        public const string Dashboard = nameof(Dashboard);
        public const string Office = nameof(Office);
        public const string Archive = nameof(Archive);
        public const string ItService = nameof(ItService);
        public const string Fleet = nameof(Fleet);
        public const string Warehouse = nameof(Warehouse);

        // Phase 7 — модули, появляющиеся вместе с СЭД-функционалом.
        public const string MyTasks = nameof(MyTasks);
        public const string Nomenclature = nameof(Nomenclature);
        public const string AuditJournal = nameof(AuditJournal);

        // Phase 8 — журналы регистрации, поиск и отчётность СЭД.
        public const string Journals = nameof(Journals);
        public const string Search = nameof(Search);
        public const string Reports = nameof(Reports);

        // Phase 9 / 11 / 12 — рабочий стол, оргструктура, замещения, настройки уведомлений.
        public const string MyDesktop = nameof(MyDesktop);
        public const string OrgStructure = nameof(OrgStructure);
        public const string Substitutions = nameof(Substitutions);
        public const string NotificationPrefs = nameof(NotificationPrefs);

        public static bool AutoRegisterOnSave { get; set; } = true;

        private static readonly IReadOnlyDictionary<EmployeeRole, HashSet<string>> _allowed =
            new Dictionary<EmployeeRole, HashSet<string>>
            {
                [EmployeeRole.Admin] = new HashSet<string>
                {
                    Dashboard, MyDesktop, Office, Archive, ItService, Fleet, Warehouse,
                    MyTasks, Nomenclature, AuditJournal,
                    Journals, Search, Reports,
                    OrgStructure, Substitutions, NotificationPrefs
                },
                [EmployeeRole.Manager] = new HashSet<string>
                {
                    Dashboard, MyDesktop, Office, Archive, ItService, Fleet, Warehouse,
                    MyTasks, Nomenclature,
                    Journals, Search, Reports,
                    Substitutions, NotificationPrefs
                },
                [EmployeeRole.Archivist] = new HashSet<string>
                {
                    Dashboard, MyDesktop, Archive, MyTasks, Nomenclature,
                    Journals, Search, Reports,
                    Substitutions, NotificationPrefs
                },
                [EmployeeRole.TechSupport] = new HashSet<string>
                {
                    Dashboard, MyDesktop, ItService, MyTasks, Search,
                    Substitutions, NotificationPrefs
                },
                [EmployeeRole.WarehouseManager] = new HashSet<string>
                {
                    Dashboard, MyDesktop, Office, Fleet, Warehouse, MyTasks, Search, Reports,
                    Substitutions, NotificationPrefs
                },
            };

        /// <summary>
        /// True, если сотруднику с данной ролью виден модуль <paramref name="moduleKey"/>.
        /// Ключи — константы этого класса.
        /// </summary>
        public static bool IsAllowed(EmployeeRole role, string moduleKey)
        {
            return _allowed.TryGetValue(role, out var set) && set.Contains(moduleKey);
        }

        // ---------------- Поведенческие разрешения (Phase 8–12) ----------------

        /// <summary>Право подписать документ простой/усиленной ЭП.</summary>
        public static bool CanSign(EmployeeRole r)
            => r == EmployeeRole.Admin || r == EmployeeRole.Manager;

        /// <summary>Право подписать документ квалифицированной ЭП (КЭП).</summary>
        public static bool CanSignQualified(EmployeeRole r)
            => r == EmployeeRole.Admin || r == EmployeeRole.Manager;

        /// <summary>Управление иерархией отделов и руководителями.</summary>
        public static bool CanManageOrgStructure(EmployeeRole r)
            => r == EmployeeRole.Admin;

        /// <summary>Создание замещения (Phase 11).</summary>
        public static bool CanCreateSubstitution(EmployeeRole r)
            => r == EmployeeRole.Admin || r == EmployeeRole.Manager;

        /// <summary>Доступ к разделу «Отчёты».</summary>
        public static bool CanViewReports(EmployeeRole r)
            => r == EmployeeRole.Admin || r == EmployeeRole.Manager || r == EmployeeRole.Archivist;

        /// <summary>
        /// Отмена связанной операции, привязанной к РКК (компенсирующее
        /// списание ТМЦ или удаление путевого листа). Доступно администратору,
        /// руководителю и материально-ответственному за ТМЦ / автопарк.
        /// </summary>
        public static bool CanCancelRelatedOperation(EmployeeRole r)
            => r == EmployeeRole.Admin
               || r == EmployeeRole.Manager
               || r == EmployeeRole.WarehouseManager;

        /// <summary>
        /// Перестроение полнотекстового индекса (Phase 10). Тяжёлая операция,
        /// разрешена только администратору.
        /// </summary>
        public static bool CanRebuildSearchIndex(EmployeeRole r)
            => r == EmployeeRole.Admin;

        /// <summary>Полнотекстовый поиск доступен всем сотрудникам.</summary>
        public static bool CanFullTextSearch(EmployeeRole r) => true;

        /// <summary>Создание собственных и общих сохранённых поисков.</summary>
        public static bool CanManageSavedSearches(EmployeeRole r) => true;

        /// <summary>Каждый сотрудник управляет своими предпочтениями уведомлений.</summary>
        public static bool CanManageNotificationPrefs(EmployeeRole r) => true;
    }
}
