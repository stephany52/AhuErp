using System;
using System.Collections.Generic;
using AhuErp.Core.Data;
using AhuErp.Core.Services;
using AhuErp.UI.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace AhuErp.UI.Infrastructure
{
    /// <summary>
    /// Корневой композишн-рут. Регистрирует сервисы и ViewModel-ы в DI-контейнере
    /// и даёт к нему статический доступ из App.xaml.cs (строго как единый entry-point).
    /// Phase 6: репозитории работают через EF6 (<see cref="AhuDbContext"/>) поверх
    /// SQL Server, схема создаётся скриптом <c>scripts/create-db.sql</c>.
    /// </summary>
    public static class AppServices
    {
        private static IServiceProvider _provider;

        public static IServiceProvider Provider =>
            _provider ?? throw new InvalidOperationException(
                "AppServices не инициализирован. Вызовите AppServices.Initialize() перед использованием.");

        public static void Initialize()
        {
            if (_provider != null) return;

            var services = new ServiceCollection();
            ConfigureServices(services);
            _provider = services.BuildServiceProvider();

            // Минимальный сидинг — только администратор, чтобы можно было войти при
            // пустой БД (схема накатывается извне через scripts/create-db.sql).
            var ctx = _provider.GetRequiredService<AhuDbContext>();
            var hasher = _provider.GetRequiredService<IPasswordHasher>();
            EfDataSeeder.EnsureSeeded(ctx, hasher);
        }

        public static T GetRequiredService<T>() where T : class =>
            Provider.GetRequiredService<T>();

        private static void ConfigureServices(IServiceCollection services)
        {
            // EF6 контекст — singleton: WPF-приложение однопользовательское, обращения
            // идут с UI-потока (фоновые задачи делают снимок коллекций до Task.Run,
            // см. DashboardViewModel). Контекст-как-singleton избавляет от ручного
            // attach/detach при последовательных операциях над одной сущностью.
            services.AddSingleton<AhuDbContext>(sp => new AhuDbContext());

            // Core services — все репозитории теперь EF6, реализации In-Memory
            // остаются в кодовой базе для тестов (используются напрямую, не через DI).
            services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher(iterations: 10_000));
            services.AddSingleton<IEmployeeRepository, EfEmployeeRepository>();
            services.AddSingleton<IDocumentRepository, EfDocumentRepository>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<ArchiveService>();
            services.AddSingleton<IInventoryRepository, EfInventoryRepository>();
            services.AddSingleton<IInventoryService, InventoryService>();
            services.AddSingleton<IVehicleRepository, EfVehicleRepository>();
            services.AddSingleton<IFleetService>(sp => new FleetService(sp.GetRequiredService<IVehicleRepository>()));
            // ReportService: расширенный конструктор с EDMS-сервисами регистрируется
            // ниже, после ITaskService и INomenclatureRepository.

            // Phase 7: enterprise EDMS-сервисы. Все построены поверх единого
            // AhuDbContext-singleton, доступ из UI-потока.
            services.AddSingleton<IAuditLogRepository, EfAuditLogRepository>();
            services.AddSingleton<IAuditService, AuditService>();

            services.AddSingleton<INomenclatureRepository, EfNomenclatureRepository>();
            services.AddSingleton<INomenclatureService, NomenclatureService>();

            services.AddSingleton<IFileStorageService>(sp => new FileSystemStorageService());
            services.AddSingleton<IAttachmentRepository, EfAttachmentRepository>();
            services.AddSingleton<IAttachmentService, AttachmentService>();

            // Phase 8 — электронные подписи и блокировка документа.
            services.AddSingleton<ISignatureRepository, EfSignatureRepository>();
            services.AddSingleton<ICryptoProvider>(sp => new HmacCryptoProvider());
            services.AddSingleton<ISignatureService>(sp => new SignatureService(
                sp.GetRequiredService<ISignatureRepository>(),
                sp.GetRequiredService<IDocumentRepository>(),
                sp.GetRequiredService<IAttachmentRepository>(),
                sp.GetRequiredService<IEmployeeRepository>(),
                sp.GetRequiredService<IAuditService>(),
                hmac: sp.GetRequiredService<ICryptoProvider>(),
                qualified: new CryptoProStub()));

            services.AddSingleton<ITaskRepository, EfTaskRepository>();
            services.AddSingleton<IWorkflowService, WorkflowService>();
            services.AddSingleton<ITaskService, TaskService>();

            // Phase 11 — оргструктура и замещения. Должны быть зарегистрированы
            // ДО TaskService/ApprovalService, т.к. оба перенаправляют исполнителя
            // через ISubstitutionService при наличии активного замещения.
            services.AddSingleton<ISubstitutionRepository, EfSubstitutionRepository>();
            services.AddSingleton<ISubstitutionService, SubstitutionService>();
            services.AddSingleton<IDelegationRepository, EfDelegationRepository>();
            services.AddSingleton<IDelegationService, DelegationService>();

            services.AddSingleton<IApprovalRepository, EfApprovalRepository>();
            services.AddSingleton<IApprovalService, ApprovalService>();

            // Phase 9 — уведомления. NoOpEmailGateway по умолчанию; реальный
            // SmtpEmailGateway включается через ключ App.config в проде.
            services.AddSingleton<INotificationRepository, EfNotificationRepository>();
            services.AddSingleton<IEmailGateway, NoOpEmailGateway>();
            services.AddSingleton<INotificationService, NotificationService>();

            // Phase 10 — полнотекстовый поиск и сохранённые фильтры.
            services.AddSingleton<ISearchIndexRepository, EfSearchIndexRepository>();
            services.AddSingleton<ISavedSearchRepository, EfSavedSearchRepository>();
            services.AddSingleton<IEnumerable<ITextExtractor>>(sp => new ITextExtractor[]
            {
                new PdfTextExtractor(),
                new DocxTextExtractor(),
                new PlainTextExtractor(),
            });
            services.AddSingleton<ISearchIndexService>(sp => new SearchIndexService(
                sp.GetRequiredService<ISearchIndexRepository>(),
                sp.GetRequiredService<IAttachmentRepository>(),
                sp.GetRequiredService<IDocumentRepository>(),
                sp.GetRequiredService<IFileStorageService>(),
                sp.GetRequiredService<IEnumerable<ITextExtractor>>()));
            services.AddSingleton<ISavedSearchService>(sp => new SavedSearchService(
                sp.GetRequiredService<ISavedSearchRepository>(),
                sp.GetRequiredService<IAuditService>()));

            // Phase 14 — каталог оборудования, журнал диагностики, ВКС, KPI ИТО.
            services.AddSingleton<IEquipmentRepository, EfEquipmentRepository>();
            services.AddSingleton<INetworkSegmentRepository, EfNetworkSegmentRepository>();
            services.AddSingleton<IVideoConferenceRepository, EfVideoConferenceRepository>();
            services.AddSingleton<IItTicketDiagnosticRepository, EfItTicketDiagnosticRepository>();
            services.AddSingleton<IItServiceMetricsProvider, ItServiceMetricsProvider>();

            services.AddSingleton<IReportService>(sp => new ReportService(
                sp.GetRequiredService<IInventoryRepository>(),
                sp.GetRequiredService<IDocumentRepository>(),
                sp.GetRequiredService<ITaskService>(),
                sp.GetRequiredService<ITaskRepository>(),
                sp.GetRequiredService<INomenclatureRepository>(),
                sp.GetRequiredService<IVehicleRepository>(),
                sp.GetRequiredService<IAuditService>()));

            // UI-инфраструктура
            services.AddSingleton<IFileDialogService, FileDialogService>();
            services.AddSingleton<DocumentNavigator>();
            services.AddSingleton<IDocumentNavigator>(sp => sp.GetRequiredService<DocumentNavigator>());

            // Bug #2 — кросс-VM шина сообщений (MyDesktop → Main для бейджа
            // непрочитанных уведомлений). WeakReferenceMessenger.Default —
            // потокобезопасный singleton от CommunityToolkit.Mvvm.
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

            // ViewModels — transient, чтобы получать свежее состояние при навигации.
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<OfficeViewModel>();
            services.AddTransient<ArchiveViewModel>();
            services.AddTransient<ItServiceViewModel>();
            services.AddTransient<FleetViewModel>();
            services.AddTransient<WarehouseViewModel>();
            services.AddTransient<RkkViewModel>();
            services.AddTransient<MyTasksViewModel>();
            services.AddTransient<NomenclatureViewModel>();
            services.AddTransient<AuditJournalViewModel>();
            services.AddTransient<JournalViewModel>();
            services.AddTransient<SearchViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<OrgStructureViewModel>();
            services.AddTransient<SubstitutionsViewModel>();
            services.AddTransient<MyDesktopViewModel>();
            services.AddTransient<NotificationPrefsViewModel>();
        }
    }
}
