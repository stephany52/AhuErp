using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// ViewModel раздела «Служба по информационно-техническому обеспечению»
    /// (Phase 14 / Improvement #10). Помимо CRUD по <see cref="ItTicket"/>
    /// и списания расходников, поддерживает:
    /// <list type="bullet">
    /// <item>каталог <see cref="Equipment"/> с привязкой к заявкам;</item>
    /// <item>журнал диагностики (<see cref="ItTicketDiagnosticEntry"/>);</item>
    /// <item>передачу в сервис (поля поставщика и срок возврата);</item>
    /// <item>KPI-плитки на дашборде ИТО (открытые / в работе /
    /// просрочено / средний MTTR).</item>
    /// </list>
    /// </summary>
    public partial class ItServiceViewModel : ViewModelBase
    {
        private readonly IDocumentRepository _documents;
        private readonly IInventoryRepository _inventory;
        private readonly IInventoryService _inventoryService;
        private readonly IAuthService _auth;
        private readonly IEquipmentRepository _equipment;
        private readonly IItTicketDiagnosticRepository _diagnostics;
        private readonly IItServiceMetricsProvider _metrics;

        public ObservableCollection<ItTicket> Tickets { get; }
        public ObservableCollection<InventoryItem> Items { get; }
        public ObservableCollection<Equipment> EquipmentCatalog { get; }
        public ObservableCollection<ItTicketDiagnosticEntry> DiagnosticEntries { get; }

        public DocumentStatus[] Statuses { get; } =
            (DocumentStatus[])Enum.GetValues(typeof(DocumentStatus));

        public ItTicketKind[] Kinds { get; } =
            (ItTicketKind[])Enum.GetValues(typeof(ItTicketKind));

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResolveCommand))]
        [NotifyCanExecuteChangedFor(nameof(AddDiagnosticCommand))]
        [NotifyCanExecuteChangedFor(nameof(SendToVendorCommand))]
        [NotifyCanExecuteChangedFor(nameof(ReturnFromVendorCommand))]
        private ItTicket selectedTicket;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string draftTitle;

        [ObservableProperty]
        private string draftAffectedEquipment;

        [ObservableProperty]
        private Equipment draftEquipmentRef;

        [ObservableProperty]
        private ItTicketKind draftKind = ItTicketKind.HardwareRepair;

        [ObservableProperty]
        private string draftResolutionNotes;

        [ObservableProperty]
        private DocumentStatus draftStatus = DocumentStatus.New;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResolveCommand))]
        private InventoryItem consumedItem;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResolveCommand))]
        private int consumedQuantity;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddDiagnosticCommand))]
        private string diagnosticAction;

        [ObservableProperty]
        private string diagnosticCategory;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendToVendorCommand))]
        private string vendorName;

        [ObservableProperty]
        private string vendorTicketNumber;

        [ObservableProperty]
        private DateTime? vendorReturnDeadline;

        [ObservableProperty]
        private string errorMessage;

        [ObservableProperty]
        private string statusMessage;

        // ---- KPI tiles --------------------------------------------------------

        [ObservableProperty]
        private int kpiOpenCount;

        [ObservableProperty]
        private int kpiInProgressCount;

        [ObservableProperty]
        private int kpiOverdueCount;

        [ObservableProperty]
        private int kpiSentToVendorCount;

        [ObservableProperty]
        private string kpiMeanTimeToResolve = "—";

        public ItServiceViewModel(IDocumentRepository documents,
                                  IInventoryRepository inventory,
                                  IInventoryService inventoryService,
                                  IAuthService auth,
                                  IEquipmentRepository equipment,
                                  IItTicketDiagnosticRepository diagnostics,
                                  IItServiceMetricsProvider metrics)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

            Tickets = new ObservableCollection<ItTicket>();
            Items = new ObservableCollection<InventoryItem>();
            EquipmentCatalog = new ObservableCollection<Equipment>();
            DiagnosticEntries = new ObservableCollection<ItTicketDiagnosticEntry>();
            Reload();
        }

        partial void OnSelectedTicketChanged(ItTicket value)
        {
            ErrorMessage = null;
            StatusMessage = null;
            if (value == null)
            {
                DraftTitle = null;
                DraftAffectedEquipment = null;
                DraftEquipmentRef = null;
                DraftKind = ItTicketKind.HardwareRepair;
                DraftResolutionNotes = null;
                DraftStatus = DocumentStatus.New;
                VendorName = null;
                VendorTicketNumber = null;
                VendorReturnDeadline = null;
                DiagnosticEntries.Clear();
                return;
            }
            DraftTitle = value.Title;
            DraftAffectedEquipment = value.AffectedEquipment;
            DraftEquipmentRef = value.AffectedEquipmentId.HasValue
                ? EquipmentCatalog.FirstOrDefault(e => e.Id == value.AffectedEquipmentId.Value)
                : null;
            DraftKind = value.Kind;
            DraftResolutionNotes = value.ResolutionNotes;
            DraftStatus = value.Status;
            VendorName = value.VendorName;
            VendorTicketNumber = value.VendorTicketNumber;
            VendorReturnDeadline = value.VendorReturnDeadline;

            DiagnosticEntries.Clear();
            foreach (var d in _diagnostics.ListByTicket(value.Id))
                DiagnosticEntries.Add(d);
        }

        [RelayCommand]
        private void New()
        {
            SelectedTicket = null;
            DraftTitle = string.Empty;
            DraftAffectedEquipment = string.Empty;
            DraftEquipmentRef = null;
            DraftKind = ItTicketKind.HardwareRepair;
            DraftResolutionNotes = string.Empty;
            DraftStatus = DocumentStatus.New;
            ConsumedItem = null;
            ConsumedQuantity = 0;
            VendorName = null;
            VendorTicketNumber = null;
            VendorReturnDeadline = null;
            ErrorMessage = null;
            StatusMessage = null;
            DiagnosticEntries.Clear();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                if (SelectedTicket == null)
                {
                    var ticket = new ItTicket
                    {
                        Title = DraftTitle,
                        AffectedEquipment = DraftAffectedEquipment,
                        AffectedEquipmentId = DraftEquipmentRef?.Id,
                        Kind = DraftKind,
                        ResolutionNotes = DraftResolutionNotes,
                        CreationDate = DateTime.Now,
                        Deadline = DateTime.Now.AddDays(7),
                        Status = DraftStatus,
                    };
                    _documents.Add(ticket);
                }
                else
                {
                    SelectedTicket.Title = DraftTitle;
                    SelectedTicket.AffectedEquipment = DraftAffectedEquipment;
                    SelectedTicket.AffectedEquipmentId = DraftEquipmentRef?.Id;
                    SelectedTicket.Kind = DraftKind;
                    SelectedTicket.ResolutionNotes = DraftResolutionNotes;
                    SelectedTicket.Status = DraftStatus;
                    _documents.Update(SelectedTicket);
                }
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void Delete()
        {
            if (SelectedTicket == null) return;
            _documents.Remove(SelectedTicket.Id);
            Reload();
            New();
        }

        [RelayCommand(CanExecute = nameof(CanResolve))]
        private void Resolve()
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                var user = _auth.CurrentEmployee
                    ?? throw new InvalidOperationException("Пользователь не аутентифицирован.");

                if (ConsumedItem != null && ConsumedQuantity > 0)
                {
                    _inventoryService.ProcessTransaction(
                        itemId: ConsumedItem.Id,
                        quantityChange: -ConsumedQuantity,
                        documentId: SelectedTicket.Id,
                        userId: user.Id);
                }

                SelectedTicket.Status = DocumentStatus.Completed;
                SelectedTicket.CompletedAt = DateTime.Now;
                SelectedTicket.IsSentToVendor = false;
                SelectedTicket.ResolutionNotes = DraftResolutionNotes;
                _documents.Update(SelectedTicket);

                StatusMessage = ConsumedItem == null
                    ? $"Заявка #{SelectedTicket.Id} закрыта."
                    : $"Заявка #{SelectedTicket.Id} закрыта, списано {ConsumedQuantity} × «{ConsumedItem.Name}».";

                ConsumedItem = null;
                ConsumedQuantity = 0;
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand(CanExecute = nameof(CanAddDiagnostic))]
        private void AddDiagnostic()
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                var user = _auth.CurrentEmployee
                    ?? throw new InvalidOperationException("Пользователь не аутентифицирован.");

                var entry = new ItTicketDiagnosticEntry
                {
                    TicketId = SelectedTicket.Id,
                    AuthorId = user.Id,
                    Timestamp = DateTime.Now,
                    Action = DiagnosticAction,
                    Category = string.IsNullOrWhiteSpace(DiagnosticCategory) ? null : DiagnosticCategory,
                };
                _diagnostics.Add(entry);

                DiagnosticEntries.Insert(0, entry);
                DiagnosticAction = string.Empty;
                DiagnosticCategory = string.Empty;
                StatusMessage = "Запись журнала диагностики добавлена.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand(CanExecute = nameof(CanSendToVendor))]
        private void SendToVendor()
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                SelectedTicket.IsSentToVendor = true;
                SelectedTicket.VendorName = VendorName;
                SelectedTicket.VendorTicketNumber = VendorTicketNumber;
                SelectedTicket.VendorReturnDeadline = VendorReturnDeadline;
                // Используем OnHold + флаг IsSentToVendor: для UI/метрик
                // ключевым признаком «передан в сервис» является именно флаг,
                // а статус OnHold семантически точно соответствует ожиданию
                // внешней реакции от подрядчика.
                SelectedTicket.Status = DocumentStatus.OnHold;
                _documents.Update(SelectedTicket);

                StatusMessage = $"Заявка #{SelectedTicket.Id} передана в сервис «{VendorName}».";
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand(CanExecute = nameof(CanReturnFromVendor))]
        private void ReturnFromVendor()
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                SelectedTicket.IsSentToVendor = false;
                SelectedTicket.Status = DocumentStatus.InProgress;
                _documents.Update(SelectedTicket);

                StatusMessage = $"Заявка #{SelectedTicket.Id} возвращена из сервиса.";
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private bool CanSave() => !string.IsNullOrWhiteSpace(DraftTitle);
        private bool HasSelection() => SelectedTicket != null;

        private bool CanResolve() =>
            SelectedTicket != null
            && (ConsumedItem == null || ConsumedQuantity > 0);

        private bool CanAddDiagnostic() =>
            SelectedTicket != null
            && !string.IsNullOrWhiteSpace(DiagnosticAction);

        private bool CanSendToVendor() =>
            SelectedTicket != null
            && !SelectedTicket.IsSentToVendor
            && !string.IsNullOrWhiteSpace(VendorName);

        private bool CanReturnFromVendor() =>
            SelectedTicket != null
            && SelectedTicket.IsSentToVendor;

        private void Reload()
        {
            var ticketId = SelectedTicket?.Id;

            EquipmentCatalog.Clear();
            foreach (var e in _equipment.List().OrderBy(e => e.InventoryNumber))
                EquipmentCatalog.Add(e);

            Tickets.Clear();
            foreach (var t in _documents.ListItTickets().OrderByDescending(t => t.CreationDate))
                Tickets.Add(t);

            Items.Clear();
            foreach (var i in _inventory.ListItems().OrderBy(i => i.Name))
                Items.Add(i);

            SelectedTicket = Tickets.FirstOrDefault(t => t.Id == ticketId);
            RecomputeKpi();
        }

        private void RecomputeKpi()
        {
            var snapshot = _metrics.Compute();
            KpiOpenCount = snapshot.OpenCount;
            KpiInProgressCount = snapshot.InProgressCount;
            KpiOverdueCount = snapshot.OverdueCount;
            KpiSentToVendorCount = snapshot.SentToVendorCount;
            KpiMeanTimeToResolve = snapshot.MeanTimeToResolve.HasValue
                ? FormatMttr(snapshot.MeanTimeToResolve.Value)
                : "—";
        }

        internal static string FormatMttr(TimeSpan ts)
        {
            // Удобнее видеть «1 д 04:30» или «04:30», чем .NET-овские
            // 1.04:30:00. Формат подобран под русскую локаль.
            if (ts.TotalDays >= 1.0)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} д {1:D2}:{2:D2}",
                    (int)ts.TotalDays,
                    ts.Hours,
                    ts.Minutes);
            }
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:D2}:{1:D2}",
                (int)ts.TotalHours,
                ts.Minutes);
        }
    }
}
