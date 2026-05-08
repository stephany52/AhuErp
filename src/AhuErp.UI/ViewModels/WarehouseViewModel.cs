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
    /// ViewModel раздела «Склад / ТМЦ». Реализует приход и расход позиций
    /// через <see cref="IInventoryService"/>; расход обязательно привязывается
    /// к документу-основанию (внутренний приказ или IT-заявка).
    /// </summary>
    public partial class WarehouseViewModel : ViewModelBase
    {
        private readonly IInventoryRepository _inventory;
        private readonly IInventoryService _inventoryService;
        private readonly IDocumentRepository _documents;
        private readonly IAuthService _auth;
        private readonly IReportService _reports;
        private readonly IFileDialogService _fileDialog;
        private readonly IEmployeeRepository _employees;

        public ObservableCollection<InventoryItem> Items { get; }

        public ObservableCollection<Document> EligibleDocuments { get; }

        public ObservableCollection<InventoryTransaction> RecentTransactions { get; }

        public InventoryCategory[] Categories { get; } =
            (InventoryCategory[])Enum.GetValues(typeof(InventoryCategory));

        // Bug #5. Источник для фильтра «Категория» — все категории + псевдо-NULL,
        // чтобы пользователь мог быстро сбросить фильтр одним кликом.
        public IReadOnlyList<InventoryCategory?> CategoryFilterOptions { get; } =
            new List<InventoryCategory?> { null }
                .Concat(((InventoryCategory[])Enum.GetValues(typeof(InventoryCategory)))
                            .Cast<InventoryCategory?>())
                .ToList();

        // Bug #5. Источник для фильтра «Инициатор». Заполняется в Reload()
        // из набора фактических авторов транзакций (а не из всего штата),
        // чтобы выпадающий список был коротким и осмысленным.
        public ObservableCollection<Employee> InitiatorOptions { get; }
            = new ObservableCollection<Employee>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeductCommand))]
        [NotifyCanExecuteChangedFor(nameof(RestockCommand))]
        private InventoryItem selectedItem;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeductCommand))]
        [NotifyCanExecuteChangedFor(nameof(RestockCommand))]
        private int quantity = 1;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeductCommand))]
        private Document selectedDocument;

        [ObservableProperty]
        private string errorMessage;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddItemCommand))]
        private string newItemName;

        [ObservableProperty]
        private InventoryCategory newItemCategory = InventoryCategory.Stationery;

        [ObservableProperty]
        private string newItemUnit = "шт.";

        [ObservableProperty]
        private int newItemMinimumBalance;

        // Bug #5. Фильтры журнала движений. Применяются в Reload() поверх
        // выборки из репозитория (репозиторий уже отдаёт ВСЕ движения вместе
        // с навигациями Item/Document/Initiator); ограничение Take(20)
        // применяется уже после фильтрации.
        [ObservableProperty]
        private DateTime? filterFrom;

        [ObservableProperty]
        private DateTime? filterTo;

        [ObservableProperty]
        private InventoryCategory? filterCategory;

        [ObservableProperty]
        private Employee filterInitiator;

        public WarehouseViewModel(IInventoryRepository inventory,
                                  IInventoryService inventoryService,
                                  IDocumentRepository documents,
                                  IAuthService auth,
                                  IReportService reports,
                                  IFileDialogService fileDialog,
                                  IEmployeeRepository employees)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _reports = reports ?? throw new ArgumentNullException(nameof(reports));
            _fileDialog = fileDialog ?? throw new ArgumentNullException(nameof(fileDialog));
            _employees = employees ?? throw new ArgumentNullException(nameof(employees));

            Items = new ObservableCollection<InventoryItem>();
            EligibleDocuments = new ObservableCollection<Document>();
            RecentTransactions = new ObservableCollection<InventoryTransaction>();
            Reload();
        }

        [RelayCommand(CanExecute = nameof(CanDeduct))]
        private void Deduct()
        {
            Apply(-Math.Abs(Quantity), requireDocument: true,
                  successText: $"Списано {Quantity} × «{SelectedItem.Name}».");
        }

        [RelayCommand(CanExecute = nameof(CanRestock))]
        private void Restock()
        {
            Apply(Math.Abs(Quantity), requireDocument: false,
                  successText: $"Приход {Quantity} × «{SelectedItem.Name}».");
        }

        [RelayCommand]
        private void Refresh() => Reload();

        // Bug #5. Сброс всех фильтров журнала движений за один клик.
        [RelayCommand]
        private void ClearFilters()
        {
            FilterFrom = null;
            FilterTo = null;
            FilterCategory = null;
            FilterInitiator = null;
            Reload();
        }

        partial void OnFilterFromChanged(DateTime? value) => Reload();
        partial void OnFilterToChanged(DateTime? value) => Reload();
        partial void OnFilterCategoryChanged(InventoryCategory? value) => Reload();
        partial void OnFilterInitiatorChanged(Employee value) => Reload();

        [RelayCommand(CanExecute = nameof(CanAddItem))]
        private void AddItem()
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                if (NewItemMinimumBalance < 0)
                    throw new InvalidOperationException("Минимальный остаток не может быть отрицательным.");
                _inventory.AddItem(new InventoryItem
                {
                    Name = NewItemName?.Trim(),
                    Category = NewItemCategory,
                    Unit = string.IsNullOrWhiteSpace(NewItemUnit) ? "шт." : NewItemUnit.Trim(),
                    MinimumBalance = NewItemMinimumBalance,
                    TotalQuantity = 0
                });
                StatusMessage = $"Добавлена позиция «{NewItemName}» ({NewItemUnit}, мин. остаток {NewItemMinimumBalance}).";
                NewItemName = null;
                NewItemCategory = InventoryCategory.Stationery;
                NewItemUnit = "шт.";
                NewItemMinimumBalance = 0;
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            ErrorMessage = null;
            StatusMessage = null;
            var path = _fileDialog.PromptSaveFile(
                title: "Экспорт остатков ТМЦ в Excel",
                filter: "Excel files (*.xlsx)|*.xlsx",
                defaultFileName: $"inventory-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                _reports.ExportInventoryToExcel(path);
                StatusMessage = $"Экспортировано: {path}";
            }
            catch (IOException ex)
            {
                ErrorMessage = $"Не удалось записать файл (возможно, он открыт в другой программе): {ex.Message}";
            }
            catch (UnauthorizedAccessException ex)
            {
                ErrorMessage = $"Нет прав для записи в указанный каталог: {ex.Message}";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private void Apply(int change, bool requireDocument, string successText)
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                var user = _auth.CurrentEmployee
                    ?? throw new InvalidOperationException("Пользователь не аутентифицирован.");
                // Приход (requireDocument=false) НЕ должен привязываться к документу,
                // даже если пользователь случайно выбрал документ в ComboBox — иначе
                // в InventoryTransaction.DocumentId окажется чужой Id и аудит-след сломается.
                int? docId = requireDocument ? SelectedDocument?.Id : null;
                _inventoryService.ProcessTransaction(SelectedItem.Id, change, docId, user.Id);
                StatusMessage = successText;
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private bool CanDeduct() =>
            SelectedItem != null
            && Quantity > 0
            && SelectedDocument != null;

        private bool CanRestock() =>
            SelectedItem != null && Quantity > 0;

        private bool CanAddItem() => !string.IsNullOrWhiteSpace(NewItemName);

        private void Reload()
        {
            var itemId = SelectedItem?.Id;
            var docId = SelectedDocument?.Id;
            var initiatorId = FilterInitiator?.Id;

            Items.Clear();
            foreach (var it in _inventory.ListItems().OrderBy(i => i.Name))
                Items.Add(it);

            EligibleDocuments.Clear();
            foreach (var doc in _documents.ListInventoryEligibleDocuments()
                                          .OrderByDescending(d => d.CreationDate))
                EligibleDocuments.Add(doc);

            // Bug #5. Репозиторий теперь жадно подгружает Item/Document/Initiator
            // (см. EfInventoryRepository.ListTransactions). Здесь применяем
            // фильтры (диапазон дат, категория позиции, инициатор) и только
            // потом обрезаем до 20 последних, чтобы не отбрасывать релевантные
            // строки из-за дешёвого Take().
            var allTransactions = _inventory.ListTransactions();

            // Источник для ComboBox «Инициатор» — только реальные авторы.
            var distinctInitiators = allTransactions
                .Where(t => t.Initiator != null
                            || (t.InitiatorId > 0 && _employees.GetById(t.InitiatorId) != null))
                .Select(t => t.Initiator ?? _employees.GetById(t.InitiatorId))
                .Where(e => e != null)
                .GroupBy(e => e.Id)
                .Select(g => g.First())
                .OrderBy(e => e.FullName)
                .ToList();

            InitiatorOptions.Clear();
            foreach (var emp in distinctInitiators)
                InitiatorOptions.Add(emp);

            FilterInitiator = InitiatorOptions.FirstOrDefault(e => e.Id == initiatorId);

            IEnumerable<InventoryTransaction> filtered = allTransactions;
            if (FilterFrom.HasValue)
                filtered = filtered.Where(t => t.TransactionDate >= FilterFrom.Value.Date);
            if (FilterTo.HasValue)
                filtered = filtered.Where(t => t.TransactionDate < FilterTo.Value.Date.AddDays(1));
            if (FilterCategory.HasValue)
                filtered = filtered.Where(t => t.InventoryItem != null
                                               && t.InventoryItem.Category == FilterCategory.Value);
            if (FilterInitiator != null)
                filtered = filtered.Where(t => t.InitiatorId == FilterInitiator.Id);

            RecentTransactions.Clear();
            foreach (var tx in filtered.Take(20))
            {
                // Подстраховка для InMemory-репо или старых данных, у которых
                // навигации могут оставаться пустыми после .Include().
                if (tx.Initiator == null && tx.InitiatorId > 0)
                    tx.Initiator = _employees.GetById(tx.InitiatorId);
                if (tx.InventoryItem == null && tx.InventoryItemId > 0)
                    tx.InventoryItem = _inventory.GetItem(tx.InventoryItemId);
                if (tx.Document == null && tx.DocumentId.HasValue)
                    tx.Document = _documents.GetById(tx.DocumentId.Value);
                RecentTransactions.Add(tx);
            }

            SelectedItem = Items.FirstOrDefault(i => i.Id == itemId) ?? Items.FirstOrDefault();
            SelectedDocument = EligibleDocuments.FirstOrDefault(d => d.Id == docId);
        }
    }
}
