using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Декларативное сопоставление <see cref="EmployeeRole"/> и разрешённых
    /// модулей навигации, плюс набор поведенческих предикатов
    /// (<c>Can*</c>) для проверок «может ли роль X выполнить действие Y».
    /// Используется и ViewModel-ем главного окна для фильтрации меню,
    /// и сервисами/командами для авторизации действий.
    /// </summary>
    /// <remarks>
    /// Матрица доступа к модулям зафиксирована в <see cref="ModuleMatrix"/>
    /// и соответствует приёмочной таблице Bug #6 / Improvement #9
    /// (МКУ «АХУ» БМР). Тонкие ограничения внутри модуля (например,
    /// что <see cref="EmployeeRole.TechSupport"/> на складе делает только
    /// списания по ИТ-заявкам) выражаются отдельными <c>Can*</c>-предикатами,
    /// чтобы не размывать общую таблицу.
    /// </remarks>
    public static class RolePolicy
    {
        // ---- Phase 1–7 модули. ----
        public const string Dashboard = nameof(Dashboard);
        public const string Office = nameof(Office);
        public const string Archive = nameof(Archive);
        public const string ItService = nameof(ItService);
        public const string Fleet = nameof(Fleet);
        public const string Warehouse = nameof(Warehouse);

        // ---- Phase 7 — модули, появляющиеся вместе с СЭД-функционалом. ----
        public const string MyTasks = nameof(MyTasks);
        public const string Nomenclature = nameof(Nomenclature);
        public const string AuditJournal = nameof(AuditJournal);

        // ---- Phase 8 — журналы регистрации, поиск и отчётность СЭД. ----
        public const string Journals = nameof(Journals);
        public const string Search = nameof(Search);
        public const string Reports = nameof(Reports);

        // ---- Phase 9 / 11 / 12 — рабочий стол, оргструктура, замещения, настройки уведомлений. ----
        public const string MyDesktop = nameof(MyDesktop);
        public const string OrgStructure = nameof(OrgStructure);
        public const string Substitutions = nameof(Substitutions);
        public const string NotificationPrefs = nameof(NotificationPrefs);

        // ---- Bug #6 / Improvement #9 — админ-панель (только Admin). ----
        public const string AdminPanel = nameof(AdminPanel);

        /// <summary>
        /// Полный список ключей модулей, известных матрице. Используется
        /// тестами для проверки «каждый модуль покрыт хотя бы одной ролью».
        /// </summary>
        public static IReadOnlyList<string> AllModuleKeys { get; } = new[]
        {
            MyDesktop, Dashboard, Office, MyTasks, Archive, Warehouse, ItService, Fleet,
            Nomenclature, Journals, Search, Reports, OrgStructure, Substitutions,
            NotificationPrefs, AuditJournal, AdminPanel,
        };

        /// <summary>
        /// Матрица «роль × модуль → доступ». Источник истины для
        /// <see cref="IsAllowed"/>. Параллельно с этой таблицей действуют
        /// поведенческие предикаты <c>Can*</c>: они описывают тонкие
        /// ограничения внутри модуля (read-only, scope «своё подразделение»,
        /// «только списания по ИТ-заявкам» и т.д.).
        /// </summary>
        private static readonly IReadOnlyDictionary<EmployeeRole, HashSet<string>> ModuleMatrix =
            new Dictionary<EmployeeRole, HashSet<string>>
            {
                // Полный доступ ко всем 17 модулям, включая админ-панель.
                [EmployeeRole.Admin] = new HashSet<string>(AllModuleKeys),

                // Руководитель: всё кроме админ-панели. Внутри модулей
                // действуют ограничения — см. CanManageOrgStructure (read-only),
                // CanCreateSubstitution (только своё подразделение и т.п.).
                [EmployeeRole.Manager] = new HashSet<string>
                {
                    MyDesktop, Dashboard, Office, MyTasks, Archive, Warehouse, ItService, Fleet,
                    Nomenclature, Journals, Search, Reports, OrgStructure, Substitutions,
                    NotificationPrefs, AuditJournal,
                },

                // Зам. руководителя: матрица идентична Manager. Если в будущем
                // потребуется отделить — поменять только эту строку.
                [EmployeeRole.DeputyHead] = new HashSet<string>
                {
                    MyDesktop, Dashboard, Office, MyTasks, Archive, Warehouse, ItService, Fleet,
                    Nomenclature, Journals, Search, Reports, OrgStructure, Substitutions,
                    NotificationPrefs, AuditJournal,
                },

                // Архивист: РКК, архив, номенклатура, журналы, поиск, отчёты,
                // оргструктура (read-only) и настройки уведомлений.
                [EmployeeRole.Archivist] = new HashSet<string>
                {
                    MyDesktop, Office, MyTasks, Archive, Nomenclature,
                    Journals, Search, Reports, OrgStructure, NotificationPrefs,
                },

                // Специалист ИТО: РКК + ИТ-заявки + узкий доступ к складу
                // (только списания по ИТ-заявкам — гейтится отдельным
                // предикатом CanWriteOffInventoryWithBasis). Доступ к
                // конфиденциальным документам ограничен по ACL — см.
                // CanAccessDocument.
                [EmployeeRole.TechSupport] = new HashSet<string>
                {
                    MyDesktop, Office, MyTasks, Warehouse, ItService,
                    Journals, Search, Reports, OrgStructure, NotificationPrefs,
                },

                // Ответственный за ТМЦ + транспорт. РКК нужен для регистрации
                // заявок на хоз. обслуживание.
                [EmployeeRole.WarehouseManager] = new HashSet<string>
                {
                    MyDesktop, Office, MyTasks, Warehouse, Fleet,
                    Journals, Search, Reports, OrgStructure, NotificationPrefs,
                },

                // Делопроизводитель: входящие/исходящие/внутренние/договоры,
                // номенклатура, поиск, отчёты, журналы. Без склада, транспорта
                // и ИТО.
                [EmployeeRole.Clerk] = new HashSet<string>
                {
                    MyDesktop, Dashboard, Office, MyTasks, Archive, Nomenclature,
                    Journals, Search, Reports, OrgStructure, NotificationPrefs,
                },

                // Кадры: только оргструктура (edit) + замещения + личный
                // кабинет. К документам общий доступ не имеет — только к своим
                // (CanAccessDocument).
                [EmployeeRole.HRAdmin] = new HashSet<string>
                {
                    MyDesktop, MyTasks, OrgStructure, Substitutions, NotificationPrefs,
                },

                // Опциональная роль «отдельный механик автопарка». Включена
                // в матрицу для готовности; не сидируется по умолчанию.
                [EmployeeRole.FleetManager] = new HashSet<string>
                {
                    MyDesktop, MyTasks, Fleet, Journals, Search, Reports, OrgStructure, NotificationPrefs,
                },
            };

        /// <summary>
        /// True, если сотруднику с данной ролью виден модуль <paramref name="moduleKey"/>.
        /// Ключи — константы этого класса.
        /// </summary>
        public static bool IsAllowed(EmployeeRole role, string moduleKey)
        {
            return ModuleMatrix.TryGetValue(role, out var set) && set.Contains(moduleKey);
        }

        /// <summary>
        /// Список ролей, у которых модуль <paramref name="moduleKey"/> в матрице.
        /// Для тестов и быстрых аудитов — позволяет одной строкой увидеть, кто
        /// видит данный модуль.
        /// </summary>
        public static IReadOnlyList<EmployeeRole> RolesAllowed(string moduleKey)
            => ModuleMatrix
                .Where(kv => kv.Value.Contains(moduleKey))
                .Select(kv => kv.Key)
                .ToList();

        // ============================================================
        // Поведенческие разрешения (Phase 8–12)
        // ============================================================

        /// <summary>Право подписать документ простой/усиленной ЭП.</summary>
        public static bool CanSign(EmployeeRole r)
            => r == EmployeeRole.Admin
               || r == EmployeeRole.Manager
               || r == EmployeeRole.DeputyHead;

        /// <summary>Право подписать документ квалифицированной ЭП (КЭП).</summary>
        public static bool CanSignQualified(EmployeeRole r)
            => r == EmployeeRole.Admin
               || r == EmployeeRole.Manager
               || r == EmployeeRole.DeputyHead;

        /// <summary>
        /// Право редактировать оргструктуру (создавать/перемещать отделы,
        /// назначать руководителей). Per Bug #6 spec: Admin — полный доступ,
        /// HRAdmin — полный доступ; остальным роли видна (read-only).
        /// </summary>
        public static bool CanManageOrgStructure(EmployeeRole r)
            => r == EmployeeRole.Admin || r == EmployeeRole.HRAdmin;

        /// <summary>
        /// Чтение оргструктуры доступно всем сотрудникам, у которых модуль
        /// в матрице.
        /// </summary>
        public static bool CanReadOrgStructure(EmployeeRole r)
            => IsAllowed(r, OrgStructure);

        /// <summary>
        /// Создание/отмена замещений. Admin и HRAdmin — без ограничений;
        /// Manager / DeputyHead — только в рамках своего подразделения
        /// (область проверяется на уровне сервиса, см. <c>SubstitutionService</c>).
        /// </summary>
        public static bool CanCreateSubstitution(EmployeeRole r)
            => r == EmployeeRole.Admin
               || r == EmployeeRole.HRAdmin
               || r == EmployeeRole.Manager
               || r == EmployeeRole.DeputyHead;

        /// <summary>
        /// Право наложить резолюцию руководителя. По регламенту учреждения —
        /// прерогатива руководителя, заместителя и администратора.
        /// </summary>
        public static bool CanIssueResolution(EmployeeRole r)
            => r == EmployeeRole.Admin
               || r == EmployeeRole.Manager
               || r == EmployeeRole.DeputyHead;

        /// <summary>
        /// Доступ к разделу «Отчёты» в принципе. Для конкретных подотчётов
        /// ИТО / ТМЦ / архивных журналов — отдельные предикаты внутри
        /// <c>ReportService</c> (узкие срезы для TechSupport / WarehouseManager
        /// / Archivist).
        /// </summary>
        public static bool CanViewReports(EmployeeRole r) => IsAllowed(r, Reports);

        /// <summary>
        /// Отмена связанной операции, привязанной к РКК (компенсирующее
        /// списание ТМЦ или удаление путевого листа). Доступно администратору,
        /// руководителю/заму и материально-ответственному за ТМЦ / автопарк.
        /// </summary>
        public static bool CanCancelRelatedOperation(EmployeeRole r)
            => r == EmployeeRole.Admin
               || r == EmployeeRole.Manager
               || r == EmployeeRole.DeputyHead
               || r == EmployeeRole.WarehouseManager
               || r == EmployeeRole.FleetManager;

        /// <summary>
        /// Перестроение полнотекстового индекса (Phase 10). Тяжёлая операция,
        /// разрешена только администратору.
        /// </summary>
        public static bool CanRebuildSearchIndex(EmployeeRole r)
            => r == EmployeeRole.Admin;

        /// <summary>
        /// Полнотекстовый поиск доступен ровно тем, у кого модуль <see cref="Search"/>
        /// в матрице. Это коррелирует со spec-таблицей и автоматически закрывает
        /// HRAdmin (поиска нет).
        /// </summary>
        public static bool CanFullTextSearch(EmployeeRole r) => IsAllowed(r, Search);

        /// <summary>Создание собственных и общих сохранённых поисков.</summary>
        public static bool CanManageSavedSearches(EmployeeRole r) => IsAllowed(r, Search);

        /// <summary>Каждый сотрудник управляет своими предпочтениями уведомлений.</summary>
        public static bool CanManageNotificationPrefs(EmployeeRole r)
            => IsAllowed(r, NotificationPrefs);

        /// <summary>
        /// Право принять входящую складскую операцию (приход, инвентаризация).
        /// TechSupport и FleetManager сюда не входят — они только списывают
        /// по своим заявкам.
        /// </summary>
        public static bool CanCreateWarehouseIncome(EmployeeRole r)
            => r == EmployeeRole.Admin
               || r == EmployeeRole.Manager
               || r == EmployeeRole.DeputyHead
               || r == EmployeeRole.WarehouseManager;

        /// <summary>
        /// Право списать ТМЦ по основанию-документу. Для TechSupport основание
        /// должно быть ИТ-заявкой — это проверяется отдельно
        /// (<see cref="CanWriteOffInventoryWithBasis"/>); здесь только говорим,
        /// что роль в принципе может списывать.
        /// </summary>
        public static bool CanWriteOffInventory(EmployeeRole r)
            => r == EmployeeRole.Admin
               || r == EmployeeRole.Manager
               || r == EmployeeRole.DeputyHead
               || r == EmployeeRole.WarehouseManager
               || r == EmployeeRole.TechSupport;

        /// <summary>
        /// Право конкретного списания ТМЦ для роли <see cref="EmployeeRole.TechSupport"/>:
        /// тип основания должен быть ИТ-заявкой. Для остальных ролей с правом
        /// списания — без ограничения по типу основания.
        /// </summary>
        public static bool CanWriteOffInventoryWithBasis(EmployeeRole r, DocumentType? basisType)
        {
            if (!CanWriteOffInventory(r)) return false;
            if (r == EmployeeRole.TechSupport)
            {
                // ИТ-специалист списывает только под ИТ-заявку. Остальные
                // основания (общие хоз. заявки и т.д.) — не его область.
                return basisType == DocumentType.It;
            }
            return true;
        }

        /// <summary>
        /// Право видеть журнал аудита. Manager / DeputyHead — только чтение
        /// (никто не может править аудит, так как он immutable hash-chain).
        /// </summary>
        public static bool CanViewAuditJournal(EmployeeRole r) => IsAllowed(r, AuditJournal);

        /// <summary>
        /// Право доступа к админ-панели — только администратор.
        /// </summary>
        public static bool CanAccessAdminPanel(EmployeeRole r)
            => r == EmployeeRole.Admin;

        /// <summary>
        /// Доступ к карточке документа на уровне строки. Используется
        /// репозиториями/сервисами при выборке для текущего пользователя.
        /// Правила:
        /// <list type="bullet">
        ///   <item><description>Admin — общий доступ ко всему (включая Confidential).</description></item>
        ///   <item><description>Manager / DeputyHead / Clerk / Archivist — общий
        ///   доступ к РКК (без скрытия) для уровней Public / Internal;
        ///   к <see cref="DocumentAccessLevel.Confidential"/> — только если
        ///   связаны с документом (автор/исполнитель/согласующий/контролёр).</description></item>
        ///   <item><description>TechSupport — РКК открыта, но видит только
        ///   ИТ-заявки (тип <see cref="DocumentType.It"/>) и те документы,
        ///   в которых он автор / исполнитель / согласующий / контролёр.</description></item>
        ///   <item><description>WarehouseManager / FleetManager — видит документы
        ///   соответствующего профиля (хоз/транспорт) и те, в которых сам
        ///   связан.</description></item>
        ///   <item><description>HRAdmin — РКК не открыта, поэтому видит только
        ///   документы, в которых он напрямую связан.</description></item>
        /// </list>
        /// </summary>
        public static bool CanAccessDocument(EmployeeRole role, Document doc, int employeeId)
        {
            if (doc == null) return false;

            // Admin — всегда. Это удобно для админ-расследований и аудитов;
            // действия Admin всё равно пишутся в неизменяемый AuditLog.
            if (role == EmployeeRole.Admin) return true;

            bool isOwnDocument = IsOwnDocument(doc, employeeId);

            // Confidential — только «свои» (Admin уже отсечён выше).
            if (doc.AccessLevel == DocumentAccessLevel.Confidential)
                return isOwnDocument;

            switch (role)
            {
                case EmployeeRole.Manager:
                case EmployeeRole.DeputyHead:
                    // Руководитель учреждения видит весь публичный/служебный поток.
                    return true;

                case EmployeeRole.Clerk:
                    // Делопроизводитель ведёт весь общий поток документов.
                    return true;

                case EmployeeRole.Archivist:
                    // Архивист видит общий поток — РКК открыта, чтобы готовить
                    // дела к передаче в архив.
                    return true;

                case EmployeeRole.TechSupport:
                    // ИТО видит все ИТ-заявки + всё, в чём он сам.
                    return doc.Type == DocumentType.It || isOwnDocument;

                case EmployeeRole.WarehouseManager:
                    // Зав. хозяйством видит хоз. заявки + транспорт + всё, в чём он сам.
                    return doc.Type == DocumentType.Fleet
                           || doc.Type == DocumentType.General
                           || isOwnDocument;

                case EmployeeRole.FleetManager:
                    // Опциональная роль механика — только транспорт + своё.
                    return doc.Type == DocumentType.Fleet || isOwnDocument;

                case EmployeeRole.HRAdmin:
                    // Кадры к общему документообороту не имеют отношения.
                    return isOwnDocument;

                default:
                    return false;
            }
        }

        private static bool IsOwnDocument(Document doc, int employeeId)
        {
            if (employeeId <= 0) return false;
            if (doc.AuthorId == employeeId) return true;
            if (doc.AssignedEmployeeId == employeeId) return true;

            if (doc.Approvals != null)
            {
                foreach (var a in doc.Approvals)
                {
                    if (a.ApproverId == employeeId) return true;
                }
            }
            if (doc.Tasks != null)
            {
                foreach (var t in doc.Tasks)
                {
                    if (t.ExecutorId == employeeId) return true;
                    if (t.ControllerId == employeeId) return true;
                }
            }
            return false;
        }
    }
}
