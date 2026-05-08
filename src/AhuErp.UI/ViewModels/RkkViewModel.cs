using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using AhuErp.UI.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Регистрационно-контрольная карточка (РКК) — центральный экран СЭД.
    /// Шесть вкладок: реквизиты, вложения, поручения и контроль, согласование,
    /// связанные хозяйственные операции, история и аудит.
    /// </summary>
    public partial class RkkViewModel : ViewModelBase
    {
        private readonly IDocumentRepository _documents;
        private readonly INomenclatureService _nomenclature;
        private readonly IAttachmentService _attachments;
        private readonly ITaskService _tasksService;
        private readonly IApprovalService _approvals;
        private readonly IAuditService _audit;
        private readonly IAuthService _auth;
        private readonly IInventoryService _inventory;
        private readonly IInventoryRepository _inventoryRepo;
        private readonly IFleetService _fleet;
        private readonly IVehicleRepository _vehicleRepo;
        private readonly ISignatureService _signatures;
        private readonly IEmployeeRepository _employeeRepo;
        private readonly IFileDialogService _fileDialog;

        public ObservableCollection<Document> Documents { get; }
            = new ObservableCollection<Document>();

        public ObservableCollection<DocumentTypeRef> DocumentTypes { get; }
            = new ObservableCollection<DocumentTypeRef>();

        public ObservableCollection<NomenclatureCase> NomenclatureCases { get; }
            = new ObservableCollection<NomenclatureCase>();

        public ObservableCollection<DocumentAttachment> Attachments { get; }
            = new ObservableCollection<DocumentAttachment>();

        public ObservableCollection<DocumentTask> Tasks { get; }
            = new ObservableCollection<DocumentTask>();

        public ObservableCollection<DocumentResolution> Resolutions { get; }
            = new ObservableCollection<DocumentResolution>();

        public ObservableCollection<DocumentApproval> Approvals { get; }
            = new ObservableCollection<DocumentApproval>();

        public ObservableCollection<AuditLog> History { get; }
            = new ObservableCollection<AuditLog>();

        public ObservableCollection<DocumentSignature> Signatures { get; }
            = new ObservableCollection<DocumentSignature>();

        public ObservableCollection<Employee> Employees { get; }
            = new ObservableCollection<Employee>();

        public ObservableCollection<ApprovalRouteTemplate> RouteTemplates { get; }
            = new ObservableCollection<ApprovalRouteTemplate>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartApprovalRouteCommand))]
        private ApprovalRouteTemplate selectedRouteTemplate;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddAttachmentVersionCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenAttachmentCommand))]
        private DocumentAttachment selectedAttachment;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CompleteTaskCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelTaskCommand))]
        [NotifyCanExecuteChangedFor(nameof(ReassignTaskCommand))]
        private DocumentTask selectedTask;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ReassignTaskCommand))]
        private Employee reassignTaskExecutor;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ApproveCommand))]
        [NotifyCanExecuteChangedFor(nameof(RejectCommand))]
        [NotifyCanExecuteChangedFor(nameof(CommentApprovalCommand))]
        private DocumentApproval selectedApproval;

        [ObservableProperty]
        private string approvalComment;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CancelInventoryWriteOffCommand))]
        private InventoryTransaction selectedInventoryTx;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CancelVehicleTripCommand))]
        private VehicleTrip selectedTrip;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SignSimpleCommand))]
        [NotifyCanExecuteChangedFor(nameof(SignQualifiedCommand))]
        [NotifyCanExecuteChangedFor(nameof(RevokeSignatureCommand))]
        private DocumentSignature selectedSignature;

        [ObservableProperty]
        private string signReason;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SignQualifiedCommand))]
        private string signCertificateThumbprint;

        public ObservableCollection<InventoryTransaction> RelatedInventoryTx { get; }
            = new ObservableCollection<InventoryTransaction>();

        public ObservableCollection<VehicleTrip> RelatedTrips { get; }
            = new ObservableCollection<VehicleTrip>();

        public ObservableCollection<InventoryItem> InventoryItems { get; }
            = new ObservableCollection<InventoryItem>();

        public ObservableCollection<Vehicle> Vehicles { get; }
            = new ObservableCollection<Vehicle>();

        // Поля диалога связанной операции «Списание ТМЦ»
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateInventoryWriteOffCommand))]
        private InventoryItem newWriteOffItem;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateInventoryWriteOffCommand))]
        private int newWriteOffQuantity = 1;

        // Поля диалога связанной операции «Путевой лист»
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateVehicleTripCommand))]
        private Vehicle newTripVehicle;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateVehicleTripCommand))]
        private DateTime newTripStart = DateTime.Today;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateVehicleTripCommand))]
        private DateTime newTripEnd = DateTime.Today.AddDays(1);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateVehicleTripCommand))]
        private string newTripDriver;

        public DocumentDirection[] Directions { get; } =
            (DocumentDirection[])Enum.GetValues(typeof(DocumentDirection));

        public DocumentAccessLevel[] AccessLevels { get; } =
            (DocumentAccessLevel[])Enum.GetValues(typeof(DocumentAccessLevel));

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
        [NotifyCanExecuteChangedFor(nameof(AddResolutionCommand))]
        [NotifyCanExecuteChangedFor(nameof(CreateInventoryWriteOffCommand))]
        [NotifyCanExecuteChangedFor(nameof(CreateVehicleTripCommand))]
        [NotifyCanExecuteChangedFor(nameof(CreateArchiveRequestCommand))]
        [NotifyCanExecuteChangedFor(nameof(CreateItTicketCommand))]
        [NotifyCanExecuteChangedFor(nameof(SignSimpleCommand))]
        [NotifyCanExecuteChangedFor(nameof(SignQualifiedCommand))]
        [NotifyCanExecuteChangedFor(nameof(UnlockDocumentCommand))]
        [NotifyPropertyChangedFor(nameof(RegistrationNumberDisplay))]
        [NotifyPropertyChangedFor(nameof(IsDocumentLocked))]
        private Document selectedDocument;

        /// <summary>
        /// Bug #3 — баннер «Документ заблокирован КЭП» должен зажигаться
        /// только когда оба условия выполнены: <see cref="Document.IsLocked"/>
        /// = true И в <see cref="Signatures"/> есть хотя бы одна активная
        /// (не отозванная) квалифицированная подпись. Раньше баннер
        /// привязывался напрямую к <c>SelectedDocument.IsLocked</c>, и
        /// при нажатии «Новый» (когда SelectedDocument=null) WPF-биндинг
        /// падал в недетерминированное поведение, из-за чего пустая
        /// карточка выглядела заблокированной.
        /// </summary>
        public bool IsDocumentLocked =>
            SelectedDocument != null
            && SelectedDocument.IsLocked
            && Signatures.Any(s => !s.IsRevoked && s.Kind == SignatureKind.Qualified);

        public string RegistrationNumberDisplay
        {
            get
            {
                var value = SelectedDocument?.RegistrationNumber;
                return string.IsNullOrWhiteSpace(value) || value.IndexOf('{') >= 0 || value.IndexOf('}') >= 0
                    ? "будет присвоен при сохранении"
                    : value;
            }
        }

        [ObservableProperty]
        private DocumentTypeRef selectedType;

        [ObservableProperty]
        private NomenclatureCase selectedCase;

        [ObservableProperty]
        private DocumentDirection selectedDirection = DocumentDirection.Internal;

        [ObservableProperty]
        private DocumentAccessLevel selectedAccessLevel = DocumentAccessLevel.Internal;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string draftTitle;

        [ObservableProperty]
        private string draftSummary;

        [ObservableProperty]
        private string draftCorrespondent;

        [ObservableProperty]
        private DateTime draftDeadline = DateTime.Today.AddDays(7);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
        private string newTaskDescription;

        // Bug #4 — отдельное поле под текст резолюции (см. вкладку
        // «3. Поручения и контроль» — секция «Резолюции руководителя»).
        // Кнопка «Наложить резолюцию» доступна только Manager/Admin.
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddResolutionCommand))]
        private string newResolutionText;

        [ObservableProperty]
        private DateTime newTaskDeadline = DateTime.Today.AddDays(3);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
        private Employee newTaskExecutor;

        [ObservableProperty]
        private string errorMessage;

        // ---------------- Bug #7. Фасеточный фильтр + пресеты РКК ----------

        public DocumentTypeFacet[] TypeFacets { get; } =
            (DocumentTypeFacet[])Enum.GetValues(typeof(DocumentTypeFacet));

        public DocumentStatusFacet[] StatusFacets { get; } =
            (DocumentStatusFacet[])Enum.GetValues(typeof(DocumentStatusFacet));

        public DocumentRoleFacet[] RoleFacets { get; } =
            (DocumentRoleFacet[])Enum.GetValues(typeof(DocumentRoleFacet));

        public DocumentDeadlineFacet[] DeadlineFacets { get; } =
            (DocumentDeadlineFacet[])Enum.GetValues(typeof(DocumentDeadlineFacet));

        [ObservableProperty]
        private DocumentTypeFacet selectedTypeFacet = DocumentTypeFacet.All;

        [ObservableProperty]
        private DocumentStatusFacet selectedStatusFacet = DocumentStatusFacet.All;

        [ObservableProperty]
        private DocumentRoleFacet selectedRoleFacet = DocumentRoleFacet.All;

        [ObservableProperty]
        private DocumentDeadlineFacet selectedDeadlineFacet = DocumentDeadlineFacet.All;

        [ObservableProperty]
        private NomenclatureCase filterCase;

        /// <summary>Полнотекстовая строка поиска в шапке РКК.</summary>
        [ObservableProperty]
        private string searchText;

        /// <summary>Включён ли поп-ап «Расширенный поиск».</summary>
        [ObservableProperty]
        private bool isAdvancedSearchOpen;

        /// <summary>Нижняя граница периода для расширенного поиска.</summary>
        [ObservableProperty]
        private DateTime? advancedFromDate;

        /// <summary>Верхняя граница периода для расширенного поиска.</summary>
        [ObservableProperty]
        private DateTime? advancedToDate;

        /// <summary>Текущий выбранный пресет (отображается в шапке).</summary>
        [ObservableProperty]
        private RkkPreset currentPreset = RkkPreset.All;

        /// <summary>
        /// Карточка-описание текущего пресета — что именно показывает РКК.
        /// Биндится в подзаголовке окна (см. RkkView.xaml).
        /// </summary>
        public string PresetDisplay
        {
            get
            {
                switch (CurrentPreset)
                {
                    case RkkPreset.OfficeDocuments: return "Документационное обеспечение";
                    case RkkPreset.MyTasks: return "Мои задачи";
                    case RkkPreset.Archive: return "Архивный отдел";
                    case RkkPreset.ItService: return "ИТО";
                    case RkkPreset.Journals: return "Журналы регистрации";
                    case RkkPreset.Search: return "Поиск";
                    default: return "Все документы";
                }
            }
        }

        public RkkViewModel(
            IDocumentRepository documents,
            INomenclatureService nomenclature,
            IAttachmentService attachments,
            ITaskService tasks,
            IApprovalService approvals,
            IAuditService audit,
            IAuthService auth,
            IInventoryService inventory,
            IInventoryRepository inventoryRepo,
            IFleetService fleet,
            IVehicleRepository vehicleRepo,
            ISignatureService signatures = null,
            IEmployeeRepository employeeRepo = null,
            IFileDialogService fileDialog = null)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _nomenclature = nomenclature ?? throw new ArgumentNullException(nameof(nomenclature));
            _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            _tasksService = tasks ?? throw new ArgumentNullException(nameof(tasks));
            _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _inventoryRepo = inventoryRepo ?? throw new ArgumentNullException(nameof(inventoryRepo));
            _fleet = fleet ?? throw new ArgumentNullException(nameof(fleet));
            _vehicleRepo = vehicleRepo ?? throw new ArgumentNullException(nameof(vehicleRepo));
            _signatures = signatures;
            _employeeRepo = employeeRepo;
            _fileDialog = fileDialog;

            Reload();
        }

        partial void OnCurrentPresetChanged(RkkPreset value) => OnPropertyChanged(nameof(PresetDisplay));

        partial void OnSelectedTypeFacetChanged(DocumentTypeFacet value) { if (!_suppressFilterReload) ApplyFilter(); }
        partial void OnSelectedStatusFacetChanged(DocumentStatusFacet value) { if (!_suppressFilterReload) ApplyFilter(); }
        partial void OnSelectedRoleFacetChanged(DocumentRoleFacet value) { if (!_suppressFilterReload) ApplyFilter(); }
        partial void OnSelectedDeadlineFacetChanged(DocumentDeadlineFacet value) { if (!_suppressFilterReload) ApplyFilter(); }
        partial void OnFilterCaseChanged(NomenclatureCase value) { if (!_suppressFilterReload) ApplyFilter(); }

        partial void OnSelectedDocumentChanged(Document value)
        {
            if (value == null)
            {
                ClearDraft();
                return;
            }
            DraftTitle = value.Title;
            DraftSummary = value.Summary;
            DraftCorrespondent = value.Correspondent;
            DraftDeadline = value.Deadline == default ? DateTime.Today.AddDays(7) : value.Deadline;
            SelectedDirection = value.Direction;
            SelectedAccessLevel = value.AccessLevel;
            SelectedType = DocumentTypes.FirstOrDefault(t => t.Id == value.DocumentTypeRefId);
            SelectedCase = NomenclatureCases.FirstOrDefault(c => c.Id == value.NomenclatureCaseId);
            ReloadAttachments();
            ReloadTasks();
            ReloadResolutions();
            ReloadApprovals();
            ReloadHistory();
            ReloadRelatedOps();
            ReloadSignatures();
        }

        [RelayCommand]
        private void Reload()
        {
            ErrorMessage = null;
            DocumentTypes.Clear();
            foreach (var t in _nomenclature.ListTypes()) DocumentTypes.Add(t);
            NomenclatureCases.Clear();
            foreach (var c in _nomenclature.ListCases()) NomenclatureCases.Add(c);
            InventoryItems.Clear();
            foreach (var i in _inventoryRepo.ListItems().OrderBy(i => i.Name))
                InventoryItems.Add(i);
            Vehicles.Clear();
            foreach (var v in _vehicleRepo.ListVehicles().OrderBy(v => v.LicensePlate))
                Vehicles.Add(v);
            Employees.Clear();
            if (_employeeRepo != null)
            {
                foreach (var e in _employeeRepo.ListAll().OrderBy(e => e.FullName))
                    Employees.Add(e);
            }
            RouteTemplates.Clear();
            foreach (var rt in _approvals.ListTemplates(activeOnly: true)
                                          .OrderBy(t => t.Name))
                RouteTemplates.Add(rt);

            // Bug #7 — выборка документов теперь идёт через DocumentFilter,
            // а не "вручную по DocumentType". Это позволяет одному и тому же
            // экрану обслуживать пресеты «Документационное обеспечение»,
            // «Мои задачи», «Архивный отдел», «ИТО», «Журналы регистрации»
            // и «Поиск».
            ApplyFilter();
        }

        /// <summary>
        /// Bug #7. Перечитывает <see cref="Documents"/> через текущий
        /// <see cref="DocumentFilter"/>, собранный из фасет-свойств VM.
        /// Учитывает клиентские пост-фильтры (<see cref="DocumentRoleFacet.Approver"/>,
        /// <see cref="DocumentRoleFacet.Signer"/>, <see cref="DocumentDeadlineFacet"/>).
        /// </summary>
        [RelayCommand]
        public void ApplyFilter()
        {
            ErrorMessage = null;
            var filter = BuildCurrentFilter();
            var meId = _auth?.CurrentEmployee?.Id;
            var search = filter.ToSearchFilter(meId);

            IEnumerable<Document> matched;
            try
            {
                matched = _documents.Search(search);
            }
            catch (NotImplementedException)
            {
                // Совместимость со старыми реализациями репозитория.
                matched = _documents.ListByType(DocumentType.Internal)
                                     .Concat(_documents.ListByType(DocumentType.Office))
                                     .Concat(_documents.ListByType(DocumentType.Incoming));
            }

            // Approver / Signer — постфильтр через подсобные репозитории.
            if (filter.MyRole == DocumentRoleFacet.Approver && meId.HasValue)
            {
                var meIdValue = meId.Value;
                matched = matched.Where(d => _approvals.ListByDocument(d.Id)
                                                       .Any(a => a.ApproverId == meIdValue));
            }
            else if (filter.MyRole == DocumentRoleFacet.Signer && meId.HasValue && _signatures != null)
            {
                var meIdValue = meId.Value;
                matched = matched.Where(d => _signatures.ListByDocument(d.Id)
                                                       .Any(s => !s.IsRevoked && s.SignerId == meIdValue));
            }

            var post = filter.ApplyClientSidePostFilters(matched, meId, DateTime.Now);

            Documents.Clear();
            foreach (var d in post.OrderByDescending(d => d.RegistrationDate ?? d.CreationDate))
                Documents.Add(d);
        }

        /// <summary>Сборка <see cref="DocumentFilter"/> из фасет-свойств VM.</summary>
        private DocumentFilter BuildCurrentFilter()
        {
            return new DocumentFilter
            {
                Type = SelectedTypeFacet,
                Status = SelectedStatusFacet,
                MyRole = SelectedRoleFacet,
                Deadline = SelectedDeadlineFacet,
                NomenclatureCaseId = FilterCase?.Id,
                SearchText = SearchText,
                PeriodFrom = AdvancedFromDate,
                PeriodTo = AdvancedToDate,
            };
        }

        /// <summary>
        /// Bug #7. Применяет сохранённый пресет фильтров (задаётся
        /// MainViewModel-ом при выборе соответствующего пункта в боковом
        /// меню). Сбрасывает фасет-свойства, проставляет нужные значения
        /// и перечитывает <see cref="Documents"/>.
        /// </summary>
        public void ApplyPreset(RkkPreset preset)
        {
            CurrentPreset = preset;
            var filter = RkkPresets.Build(preset);
            // Подавляем триггер ApplyFilter() после каждого присваивания —
            // partial-обработчики OnSelected*Changed дёргают ApplyFilter, а
            // нам нужно ровно одно перечитывание в конце.
            _suppressFilterReload = true;
            try
            {
                SelectedTypeFacet = filter.Type;
                SelectedStatusFacet = filter.Status;
                SelectedRoleFacet = filter.MyRole;
                SelectedDeadlineFacet = filter.Deadline;
                FilterCase = filter.NomenclatureCaseId.HasValue
                    ? NomenclatureCases.FirstOrDefault(c => c.Id == filter.NomenclatureCaseId.Value)
                    : null;
                IsAdvancedSearchOpen = preset == RkkPreset.Search;
            }
            finally
            {
                _suppressFilterReload = false;
            }
            ApplyFilter();
        }

        private bool _suppressFilterReload;

        /// <summary>Сбросить все фасеточные фильтры и перечитать список.</summary>
        [RelayCommand]
        private void ResetFilter()
        {
            _suppressFilterReload = true;
            try
            {
                CurrentPreset = RkkPreset.All;
                SelectedTypeFacet = DocumentTypeFacet.All;
                SelectedStatusFacet = DocumentStatusFacet.All;
                SelectedRoleFacet = DocumentRoleFacet.All;
                SelectedDeadlineFacet = DocumentDeadlineFacet.All;
                FilterCase = null;
                SearchText = null;
                AdvancedFromDate = null;
                AdvancedToDate = null;
                IsAdvancedSearchOpen = false;
            }
            finally { _suppressFilterReload = false; }
            ApplyFilter();
        }

        /// <summary>Открыть/скрыть поп-ап «Расширенный поиск».</summary>
        [RelayCommand]
        private void ToggleAdvancedSearch() => IsAdvancedSearchOpen = !IsAdvancedSearchOpen;

        /// <summary>
        /// Bug #3 — кнопка «Новый» в РКК. Раньше команда только сбрасывала
        /// <see cref="SelectedDocument"/> и драфт-поля, из-за чего баннер
        /// «Документ заблокирован КЭП» иногда оказывался виден на пустой
        /// карточке (срабатывание WPF-биндинга на null). Теперь явно строим
        /// in-memory шаблон документа со статусом <see cref="DocumentStatus.New"/>
        /// («Черновик» в текущем перечислении статусов),
        /// <see cref="ApprovalRouteStatus.Draft"/>, публичным грифом и
        /// <c>IsLocked = false</c>. Документ при этом ещё не сохраняется в БД —
        /// сохранение происходит из <see cref="Save"/>, который читает драфт-поля.
        /// </summary>
        [RelayCommand]
        private void New()
        {
            SelectedDocument = null;
            ClearDraft();
            // Явный шаблон-черновик. Используется тестами как контракт «новый
            // документ всегда разблокирован, в статусе New, с публичным грифом».
            DraftDocumentTemplate = new Document
            {
                IsLocked = false,
                Status = DocumentStatus.New,
                ApprovalStatus = ApprovalRouteStatus.Draft,
                AccessLevel = DocumentAccessLevel.Public,
                CreationDate = DateTime.Now,
            };
            SelectedAccessLevel = DocumentAccessLevel.Public;
            SelectedDirection = DocumentDirection.Internal;
        }

        /// <summary>
        /// Bug #3 — последний шаблон, созданный <see cref="New"/>. Используется
        /// тестами для проверки контракта «новая РКК всегда стартует Draft и
        /// разблокированной». В UI напрямую не биндится: реальный документ
        /// материализуется при первом <see cref="Save"/>.
        /// </summary>
        public Document DraftDocumentTemplate { get; private set; }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
            ErrorMessage = null;
            try
            {
                if (SelectedDocument == null)
                {
                    var doc = new Document
                    {
                        Title = DraftTitle,
                        Summary = DraftSummary,
                        Correspondent = DraftCorrespondent,
                        Type = MapDirectionToType(SelectedDirection),
                        Direction = SelectedDirection,
                        AccessLevel = SelectedAccessLevel,
                        CreationDate = DateTime.Now,
                        Deadline = DraftDeadline,
                        Status = DocumentStatus.New,
                        DocumentTypeRefId = SelectedType?.Id,
                        NomenclatureCaseId = SelectedCase?.Id,
                        AuthorId = _auth.CurrentEmployee?.Id
                    };
                    _documents.Add(doc);
                    _audit.Record(AuditActionType.Created, nameof(Document), doc.Id,
                        _auth.CurrentEmployee?.Id, newValues: $"Title={doc.Title}");
                    AutoRegisterOnSaveIfNeeded(doc);
                    Reload();
                    SelectedDocument = Documents.FirstOrDefault(d => d.Id == doc.Id);
                }
                else
                {
                    var doc = SelectedDocument;
                    doc.Title = DraftTitle;
                    doc.Summary = DraftSummary;
                    doc.Correspondent = DraftCorrespondent;
                    doc.Direction = SelectedDirection;
                    doc.AccessLevel = SelectedAccessLevel;
                    doc.Deadline = DraftDeadline;
                    doc.DocumentTypeRefId = SelectedType?.Id;
                    doc.NomenclatureCaseId = SelectedCase?.Id;
                    _documents.Update(doc);
                    _audit.Record(AuditActionType.Updated, nameof(Document), doc.Id,
                        _auth.CurrentEmployee?.Id, newValues: $"Title={doc.Title}");
                    if (AutoRegisterOnSaveIfNeeded(doc))
                    {
                        var docId = doc.Id;
                        Reload();
                        SelectedDocument = Documents.FirstOrDefault(d => d.Id == docId);
                    }
                    else
                    {
                        ReloadHistory();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRegister))]
        private void Register()
        {
            ErrorMessage = null;
            try
            {
                // Запоминаем Id ДО Reload(): тот очищает Documents, что в свою
                // очередь сбрасывает SelectedDocument в null через WPF-биндинг.
                var docId = SelectedDocument.Id;
                _nomenclature.Register(docId, SelectedCase?.Id);
                Reload();
                SelectedDocument = Documents.FirstOrDefault(d => d.Id == docId);
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        // Bug #4. «Наложить резолюцию» — отдельная команда для руководителя.
        // Не требует исполнителя/срока в UI: исполнитель упоминается в тексте
        // самой резолюции по формату «@ФамилияИО», и сервис создаст ему
        // in-app/e-mail уведомление. Конкретные поручения с дедлайнами
        // создаются отдельно командой AddTask.
        [RelayCommand(CanExecute = nameof(CanIssueResolution))]
        private void AddResolution()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                if (actor == 0) { ErrorMessage = "Не определён текущий сотрудник."; return; }

                _tasksService.AddResolution(SelectedDocument.Id, actor, NewResolutionText);
                NewResolutionText = null;
                ReloadResolutions();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private bool CanIssueResolution()
        {
            var role = _auth?.CurrentEmployee?.Role;
            return SelectedDocument != null
                   && !string.IsNullOrWhiteSpace(NewResolutionText)
                   && role.HasValue
                   && RolePolicy.CanIssueResolution(role.Value);
        }

        [RelayCommand(CanExecute = nameof(CanAddTask))]
        private void AddTask()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                _tasksService.CreateTask(
                    SelectedDocument.Id,
                    authorId: actor,
                    executorId: NewTaskExecutor?.Id ?? 0,
                    description: NewTaskDescription,
                    deadline: NewTaskDeadline.Date.AddDays(1).AddSeconds(-1));
                NewTaskDescription = null;
                NewTaskExecutor = null;
                ReloadTasks();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        // ── Task 7: «Создать связанную операцию» ──────────────────────────

        [RelayCommand(CanExecute = nameof(CanWriteOff))]
        private void CreateInventoryWriteOff()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                if (actor <= 0) throw new InvalidOperationException("Не определён текущий пользователь.");
                if (NewWriteOffQuantity <= 0)
                    throw new InvalidOperationException("Количество должно быть положительным.");

                var tx = _inventory.ProcessTransaction(
                    NewWriteOffItem.Id,
                    -NewWriteOffQuantity,
                    SelectedDocument.Id,
                    actor);

                _audit.Record(AuditActionType.Created, nameof(InventoryTransaction), tx.Id, actor,
                    newValues: $"BasisDocumentId={SelectedDocument.Id};Item={NewWriteOffItem.Name};Qty=-{NewWriteOffQuantity}");

                NewWriteOffItem = null;
                NewWriteOffQuantity = 1;
                ReloadRelatedOps();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(CanCreateTrip))]
        private void CreateVehicleTrip()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                if (actor <= 0) throw new InvalidOperationException("Не определён текущий пользователь.");

                var trip = _fleet.BookVehicle(
                    NewTripVehicle.Id,
                    SelectedDocument.Id,
                    NewTripStart,
                    NewTripEnd,
                    string.IsNullOrWhiteSpace(NewTripDriver) ? "—" : NewTripDriver);

                _audit.Record(AuditActionType.Created, nameof(VehicleTrip), trip.Id, actor,
                    newValues: $"BasisDocumentId={SelectedDocument.Id};Vehicle={NewTripVehicle.LicensePlate};Driver={NewTripDriver};{NewTripStart:yyyy-MM-dd}—{NewTripEnd:yyyy-MM-dd}");

                NewTripVehicle = null;
                NewTripDriver = null;
                NewTripStart = DateTime.Today;
                NewTripEnd = DateTime.Today.AddDays(1);
                ReloadRelatedOps();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedDocument))]
        private void CreateArchiveRequest()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                var req = new ArchiveRequest
                {
                    Title = $"Заявка на основании документа {SelectedDocument.RegistrationNumber ?? "#" + SelectedDocument.Id}",
                    Summary = SelectedDocument.Summary,
                    Type = DocumentType.Archive,
                    Direction = DocumentDirection.Internal,
                    AccessLevel = SelectedDocument.AccessLevel,
                    CreationDate = DateTime.Now,
                    Deadline = DateTime.Today.AddDays(30),
                    Status = DocumentStatus.New,
                    AuthorId = actor > 0 ? (int?)actor : null,
                    BasisDocumentId = SelectedDocument.Id
                };
                _documents.Add(req);
                _audit.Record(AuditActionType.Created, nameof(ArchiveRequest), req.Id, actor,
                    newValues: $"BasisDocumentId={SelectedDocument.Id}");
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedDocument))]
        private void CreateItTicket()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                var ticket = new ItTicket
                {
                    Title = $"Заявка ИТ по документу {SelectedDocument.RegistrationNumber ?? "#" + SelectedDocument.Id}",
                    Summary = SelectedDocument.Summary,
                    Type = DocumentType.It,
                    Direction = DocumentDirection.Internal,
                    AccessLevel = SelectedDocument.AccessLevel,
                    CreationDate = DateTime.Now,
                    Deadline = DateTime.Today.AddDays(7),
                    Status = DocumentStatus.New,
                    AuthorId = actor > 0 ? (int?)actor : null,
                    BasisDocumentId = SelectedDocument.Id
                };
                _documents.Add(ticket);
                _audit.Record(AuditActionType.Created, nameof(ItTicket), ticket.Id, actor,
                    newValues: $"BasisDocumentId={SelectedDocument.Id}");
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private bool AutoRegisterOnSaveIfNeeded(Document doc)
        {
            if (!RolePolicy.AutoRegisterOnSave || doc == null || doc.IsRegistered || doc.IsLocked || !doc.DocumentTypeRefId.HasValue)
                return false;

            _nomenclature.Register(doc.Id, SelectedCase?.Id ?? doc.NomenclatureCaseId);
            return true;
        }

        private bool HasSelectedDocument() => SelectedDocument != null;

        private bool CanWriteOff() =>
            SelectedDocument != null && NewWriteOffItem != null && NewWriteOffQuantity > 0;

        private bool CanCreateTrip() =>
            SelectedDocument != null && NewTripVehicle != null
            && NewTripEnd > NewTripStart;

        private void ReloadRelatedOps()
        {
            RelatedInventoryTx.Clear();
            RelatedTrips.Clear();
            if (SelectedDocument == null) return;

            foreach (var tx in _inventoryRepo.ListTransactions()
                                              .Where(t => t.BasisDocumentId == SelectedDocument.Id
                                                          || t.DocumentId == SelectedDocument.Id)
                                              .OrderByDescending(t => t.TransactionDate))
                RelatedInventoryTx.Add(tx);

            foreach (var v in _vehicleRepo.ListVehicles())
                foreach (var trip in _vehicleRepo.ListTrips(v.Id)
                                                  .Where(t => t.BasisDocumentId == SelectedDocument.Id
                                                              || t.DocumentId == SelectedDocument.Id))
                    RelatedTrips.Add(trip);
        }

        private static DocumentType MapDirectionToType(DocumentDirection dir)
        {
            switch (dir)
            {
                case DocumentDirection.Incoming: return DocumentType.Incoming;
                case DocumentDirection.Outgoing: return DocumentType.Office;
                case DocumentDirection.Directive: return DocumentType.Office;
                default: return DocumentType.Internal;
            }
        }

        private void ReloadAttachments()
        {
            Attachments.Clear();
            if (SelectedDocument == null) return;
            foreach (var a in _attachments.ListByDocument(SelectedDocument.Id)) Attachments.Add(a);
        }

        private void ReloadTasks()
        {
            Tasks.Clear();
            if (SelectedDocument == null) return;
            foreach (var t in _tasksService.ListByDocument(SelectedDocument.Id)) Tasks.Add(t);
        }

        private void ReloadResolutions()
        {
            Resolutions.Clear();
            if (SelectedDocument == null) return;
            foreach (var r in _tasksService.ListResolutionsByDocument(SelectedDocument.Id))
                Resolutions.Add(r);
        }

        private void ReloadApprovals()
        {
            Approvals.Clear();
            if (SelectedDocument == null) return;
            foreach (var a in _approvals.ListByDocument(SelectedDocument.Id)) Approvals.Add(a);
        }

        private void ReloadHistory()
        {
            History.Clear();
            if (SelectedDocument == null) return;
            var entries = _audit.Query(new AuditQueryFilter
            {
                EntityType = nameof(Document),
                EntityId = SelectedDocument.Id,
                Top = 200
            });
            foreach (var e in entries) History.Add(e);
        }

        private void ClearDraft()
        {
            DraftTitle = null;
            DraftSummary = null;
            DraftCorrespondent = null;
            DraftDeadline = DateTime.Today.AddDays(7);
            SelectedDirection = DocumentDirection.Internal;
            SelectedAccessLevel = DocumentAccessLevel.Internal;
            SelectedType = null;
            SelectedCase = null;
            Attachments.Clear();
            Tasks.Clear();
            Resolutions.Clear();
            Approvals.Clear();
            History.Clear();
            Signatures.Clear();
        }

        // ---------------- Phase 8 — подписи -------------------------------

        private void ReloadSignatures()
        {
            Signatures.Clear();
            if (SelectedDocument == null || _signatures == null)
            {
                OnPropertyChanged(nameof(IsDocumentLocked));
                UnlockDocumentCommand.NotifyCanExecuteChanged();
                return;
            }
            foreach (var s in _signatures.ListByDocument(SelectedDocument.Id))
                Signatures.Add(s);
            // Bug #3: лок-баннер зависит и от состава Signatures, и от
            // SelectedDocument.IsLocked — пересчитываем после обновления списка.
            OnPropertyChanged(nameof(IsDocumentLocked));
            UnlockDocumentCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanSign))]
        private void SignSimple()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                if (actor == 0) { ErrorMessage = "Не определён текущий сотрудник."; return; }
                _signatures.Sign(SelectedDocument.Id, attachmentId: null, signerId: actor,
                    kind: SignatureKind.Simple, reason: SignReason);
                SignReason = null;
                ReloadSignatures();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(CanSignQualified))]
        private void SignQualified()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                if (actor == 0) { ErrorMessage = "Не определён текущий сотрудник."; return; }
                _signatures.Sign(SelectedDocument.Id, attachmentId: null, signerId: actor,
                    kind: SignatureKind.Qualified, reason: SignReason,
                    certificateThumbprint: SignCertificateThumbprint);
                SignReason = null;
                SignCertificateThumbprint = null;
                ReloadSignatures();
                ReloadHistory();
                // SelectedDocument теперь IsLocked=true — обновляем из репозитория.
                if (SelectedDocument != null)
                {
                    var refreshed = _documents.GetById(SelectedDocument.Id);
                    if (refreshed != null) SelectedDocument = refreshed;
                }
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(CanRevokeSignature))]
        private void RevokeSignature()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                if (actor == 0) { ErrorMessage = "Не определён текущий сотрудник."; return; }
                _signatures.Revoke(SelectedSignature.Id, actor,
                    SignReason ?? "Отзыв из РКК");
                SignReason = null;
                ReloadSignatures();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        /// <summary>
        /// Bug #3 — снимает программную блокировку с документа (выставленную
        /// первой Qualified-подписью). Доступно только Admin/Manager:
        /// вынуждает разблокировать карточку даже если активная Qualified-
        /// подпись осталась в журнале — фактическое подписание не отменяется,
        /// просто разрешается редактировать «иммутабельные» поля. Каждое
        /// нажатие пишет запись в <see cref="AuditActionType.DocumentUnlocked"/>.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUnlockDocument))]
        private void UnlockDocument()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                if (actor == 0) { ErrorMessage = "Не определён текущий сотрудник."; return; }
                var doc = SelectedDocument;
                if (doc == null || !doc.IsLocked) return;

                doc.IsLocked = false;
                _documents.Update(doc);
                _audit.Record(AuditActionType.DocumentUnlocked, nameof(Document), doc.Id, actor,
                    newValues: "IsLocked=false", details: SignReason ?? "Снятие блокировки из РКК");
                SignReason = null;

                // Обновляем выбранный документ — поднимет PropertyChanged для всех
                // зависимых биндингов (баннер, IsReadOnly у Title/Correspondent/Summary).
                // InMemoryDocumentRepository.GetById() возвращает ту же ссылку, что
                // лежит в SelectedDocument, поэтому SetProperty не зафиксирует
                // изменения и WPF не перевычислит {Binding SelectedDocument.IsLocked}.
                // Сначала сбрасываем в null, затем переустанавливаем — это вынудит
                // WPF переподписаться на свойства документа и перерисовать поля.
                var refreshed = _documents.GetById(doc.Id);
                SelectedDocument = null;
                if (refreshed != null) SelectedDocument = refreshed;
                OnPropertyChanged(nameof(IsDocumentLocked));
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private bool CanSign() => SelectedDocument != null && _signatures != null;
        private bool CanSignQualified() => CanSign()
            && !string.IsNullOrWhiteSpace(SignCertificateThumbprint);
        private bool CanRevokeSignature() => SelectedSignature != null
            && !SelectedSignature.IsRevoked && _signatures != null;

        private bool CanUnlockDocument()
        {
            var role = _auth?.CurrentEmployee?.Role;
            return SelectedDocument != null
                   && SelectedDocument.IsLocked
                   && role.HasValue
                   && (role.Value == EmployeeRole.Admin || role.Value == EmployeeRole.Manager);
        }

        private bool CanSave() => !string.IsNullOrWhiteSpace(DraftTitle);
        private bool CanRegister() => SelectedDocument != null && !SelectedDocument.IsRegistered;
        private bool CanAddTask() => SelectedDocument != null
            && !string.IsNullOrWhiteSpace(NewTaskDescription)
            && NewTaskExecutor != null;

        // ── A4: Поручения — операции с выбранной задачей ──────────────────

        private bool IsTaskMutable(DocumentTask t) =>
            t != null && t.Status != DocumentTaskStatus.Completed
                      && t.Status != DocumentTaskStatus.Cancelled;

        private bool CanCompleteTask() => IsTaskMutable(SelectedTask);
        private bool CanCancelTask() => IsTaskMutable(SelectedTask);
        private bool CanReassignTask() => IsTaskMutable(SelectedTask)
            && ReassignTaskExecutor != null;

        [RelayCommand(CanExecute = nameof(CanCompleteTask))]
        private void CompleteTask()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                _tasksService.UpdateStatus(SelectedTask.Id, DocumentTaskStatus.Completed, actor,
                    reportText: "Отметка о выполнении из РКК.");
                ReloadTasks();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(CanCancelTask))]
        private void CancelTask()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                _tasksService.UpdateStatus(SelectedTask.Id, DocumentTaskStatus.Cancelled, actor,
                    reportText: "Отмена поручения из РКК.");
                ReloadTasks();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(CanReassignTask))]
        private void ReassignTask()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                _tasksService.Reassign(SelectedTask.Id, ReassignTaskExecutor.Id, actor,
                    reason: "Переназначение из РКК.");
                ReassignTaskExecutor = null;
                ReloadTasks();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        // ── A3: Вложения — загрузка / новая версия / открытие ─────────────

        [RelayCommand(CanExecute = nameof(HasSelectedDocument))]
        private void AddAttachment()
        {
            ErrorMessage = null;
            try
            {
                if (_fileDialog == null)
                {
                    ErrorMessage = "Диалог выбора файла недоступен.";
                    return;
                }
                var path = _fileDialog.PromptOpenFile(
                    "Загрузить вложение",
                    "Все файлы (*.*)|*.*");
                if (string.IsNullOrEmpty(path)) return;

                var actor = _auth.CurrentEmployee?.Id ?? 0;
                using (var stream = File.OpenRead(path))
                {
                    _attachments.Upload(SelectedDocument.Id, stream,
                        Path.GetFileName(path), actor);
                }
                ReloadAttachments();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private bool CanAddAttachmentVersion() =>
            SelectedAttachment != null && _fileDialog != null;

        [RelayCommand(CanExecute = nameof(CanAddAttachmentVersion))]
        private void AddAttachmentVersion()
        {
            ErrorMessage = null;
            try
            {
                var path = _fileDialog.PromptOpenFile(
                    $"Новая версия «{SelectedAttachment.FileName}»",
                    "Все файлы (*.*)|*.*");
                if (string.IsNullOrEmpty(path)) return;

                var actor = _auth.CurrentEmployee?.Id ?? 0;
                using (var stream = File.OpenRead(path))
                {
                    _attachments.AddVersion(SelectedAttachment.AttachmentGroupId,
                        stream, Path.GetFileName(path), actor);
                }
                ReloadAttachments();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private bool CanOpenAttachment() =>
            SelectedAttachment != null && _fileDialog != null;

        [RelayCommand(CanExecute = nameof(CanOpenAttachment))]
        private void OpenAttachment()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                var savePath = _fileDialog.PromptSaveFile(
                    "Сохранить копию вложения",
                    "Все файлы (*.*)|*.*",
                    SelectedAttachment.FileName);
                if (string.IsNullOrEmpty(savePath)) return;

                using (var src = _attachments.Open(SelectedAttachment.Id, actor))
                using (var dst = File.Create(savePath))
                {
                    src.CopyTo(dst);
                }
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        // ── A5: Согласование — запуск маршрута и решения ─────────────────

        private bool CanStartApprovalRoute() =>
            SelectedDocument != null
            && SelectedRouteTemplate != null
            && Approvals.Count == 0;

        [RelayCommand(CanExecute = nameof(CanStartApprovalRoute))]
        private void StartApprovalRoute()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                _approvals.StartApproval(SelectedDocument.Id, SelectedRouteTemplate.Id, actor);
                ReloadApprovals();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private bool CanDecide(ApprovalDecision _) =>
            SelectedApproval != null
            && SelectedApproval.Decision == ApprovalDecision.Pending
            && _auth.CurrentEmployee != null
            && SelectedApproval.ApproverId == _auth.CurrentEmployee.Id;

        private bool CanApprove() => CanDecide(ApprovalDecision.Approved);
        private bool CanReject() => CanDecide(ApprovalDecision.Rejected);
        private bool CanCommentApproval() => CanDecide(ApprovalDecision.Comments);

        [RelayCommand(CanExecute = nameof(CanApprove))]
        private void Approve() => ApplyDecision(ApprovalDecision.Approved);

        [RelayCommand(CanExecute = nameof(CanReject))]
        private void Reject() => ApplyDecision(ApprovalDecision.Rejected);

        [RelayCommand(CanExecute = nameof(CanCommentApproval))]
        private void CommentApproval() => ApplyDecision(ApprovalDecision.Comments);

        private void ApplyDecision(ApprovalDecision decision)
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee.Id;
                _approvals.ApplyDecision(SelectedApproval.Id, decision, actor,
                    string.IsNullOrWhiteSpace(ApprovalComment) ? null : ApprovalComment);
                ApprovalComment = null;
                ReloadApprovals();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        // ── A6: Отмена связанных операций ─────────────────────────────────

        private bool CanCancelRelatedOp()
        {
            var role = _auth.CurrentEmployee?.Role;
            return role.HasValue && RolePolicy.CanCancelRelatedOperation(role.Value);
        }

        private bool CanCancelInventoryWriteOff()
        {
            if (SelectedInventoryTx == null) return false;
            // Отмена допускается только для расходных операций, привязанных к
            // текущей карточке. Компенсация делается одной зеркальной транзакцией.
            return SelectedInventoryTx.QuantityChanged < 0
                   && SelectedInventoryTx.BasisDocumentId == SelectedDocument?.Id
                   && CanCancelRelatedOp();
        }

        [RelayCommand(CanExecute = nameof(CanCancelInventoryWriteOff))]
        private void CancelInventoryWriteOff()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee.Id;
                var compensating = _inventory.ProcessTransaction(
                    SelectedInventoryTx.InventoryItemId,
                    -SelectedInventoryTx.QuantityChanged,
                    SelectedDocument.Id,
                    actor);
                _audit.Record(AuditActionType.Deleted, nameof(InventoryTransaction),
                    SelectedInventoryTx.Id, actor,
                    newValues: $"Отменено компенсирующей транзакцией #{compensating.Id};Qty={-SelectedInventoryTx.QuantityChanged}");
                ReloadRelatedOps();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private bool CanCancelVehicleTrip() =>
            SelectedTrip != null
            && SelectedTrip.BasisDocumentId == SelectedDocument?.Id
            && CanCancelRelatedOp();

        [RelayCommand(CanExecute = nameof(CanCancelVehicleTrip))]
        private void CancelVehicleTrip()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee.Id;
                var trip = _fleet.CancelTrip(SelectedTrip.Id, actor, "Отмена из РКК.");
                _audit.Record(AuditActionType.Deleted, nameof(VehicleTrip), trip.Id, actor,
                    newValues: $"Отмена путевого листа #{trip.Id};Vehicle={trip.VehicleId}");
                ReloadRelatedOps();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }
    }
}
