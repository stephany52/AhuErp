using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using AhuErp.UI.Infrastructure;
using AhuErp.UI.ViewModels;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Bug #5. Регрессионные тесты для раздела «Склад / ТМЦ»: журнал
    /// последних движений должен показывать имена позиций и регистрационные
    /// номера документов-оснований, а не Id; фильтры по дате, категории и
    /// инициатору должны корректно сужать выборку.
    /// </summary>
    public class WarehouseViewModelTests
    {
        private readonly InMemoryInventoryRepository _inventory;
        private readonly InMemoryDocumentRepository _documents;
        private readonly StubEmployeeRepository _employees;
        private readonly StubAuthService _auth;
        private readonly StubReportService _reports;
        private readonly StubFileDialogService _fileDialog;
        private readonly InventoryService _inventoryService;

        private readonly Employee _ito;
        private readonly Employee _supply;
        private readonly InventoryItem _paper;
        private readonly InventoryItem _toner;
        private readonly Document _orderForToner;

        public WarehouseViewModelTests()
        {
            _inventory = new InMemoryInventoryRepository();
            _documents = new InMemoryDocumentRepository();
            _employees = new StubEmployeeRepository();

            _ito = new Employee { Id = 1, FullName = "Иванов И.И.", Role = EmployeeRole.TechSupport };
            _supply = new Employee { Id = 2, FullName = "Петров П.П.", Role = EmployeeRole.WarehouseManager };
            _employees.Add(_ito);
            _employees.Add(_supply);

            _auth = new StubAuthService { CurrentEmployee = _supply };
            _reports = new StubReportService();
            _fileDialog = new StubFileDialogService();
            _inventoryService = new InventoryService(_inventory);

            _paper = new InventoryItem
            {
                Name = "Бумага А4",
                Category = InventoryCategory.Stationery,
                Unit = "пачка",
                TotalQuantity = 100
            };
            _toner = new InventoryItem
            {
                Name = "Картридж HP",
                Category = InventoryCategory.IT_Equipment,
                Unit = "шт.",
                TotalQuantity = 10
            };
            _inventory.AddItem(_paper);
            _inventory.AddItem(_toner);

            _orderForToner = new Document
            {
                Title = "Распоряжение на выдачу картриджей",
                RegistrationNumber = "РАС-2026-00007",
                Type = DocumentType.Internal,
                CreationDate = DateTime.Now
            };
            _documents.Add(_orderForToner);
        }

        private WarehouseViewModel CreateVm()
            => new WarehouseViewModel(_inventory, _inventoryService, _documents,
                                      _auth, _reports, _fileDialog, _employees);

        [Fact]
        public void Reload_populates_InventoryItem_and_Document_navigations_for_each_transaction()
        {
            // Готовим живые движения через сервис: расход с документом-основанием
            // и приход без документа.
            _inventoryService.ProcessTransaction(_toner.Id, -2, _orderForToner.Id, _supply.Id);
            _inventoryService.ProcessTransaction(_paper.Id, 50, null, _supply.Id);

            var vm = CreateVm();

            Assert.Equal(2, vm.RecentTransactions.Count);
            // Все строки имеют разрешённую навигацию InventoryItem (имя позиции).
            Assert.All(vm.RecentTransactions, tx =>
            {
                Assert.NotNull(tx.InventoryItem);
                Assert.False(string.IsNullOrEmpty(tx.InventoryItem.Name));
            });

            var withDoc = vm.RecentTransactions.Single(t => t.DocumentId.HasValue);
            Assert.NotNull(withDoc.Document);
            Assert.Equal("РАС-2026-00007", withDoc.Document.RegistrationNumber);

            var withoutDoc = vm.RecentTransactions.Single(t => !t.DocumentId.HasValue);
            Assert.Null(withoutDoc.Document);
        }

        [Fact]
        public void Reload_resolves_Initiator_from_employee_repository_when_navigation_is_missing()
        {
            // InMemory-репозиторий не подгружает Initiator (это делает только
            // EF6 .Include); ViewModel обязан догружать его сам.
            _inventoryService.ProcessTransaction(_toner.Id, -1, _orderForToner.Id, _ito.Id);

            var vm = CreateVm();

            Assert.NotNull(vm.RecentTransactions[0].Initiator);
            Assert.Equal(_ito.FullName, vm.RecentTransactions[0].Initiator.FullName);
        }

        [Fact]
        public void Reload_truncates_to_twenty_most_recent_transactions()
        {
            for (int i = 0; i < 25; i++)
                _inventoryService.ProcessTransaction(_paper.Id, 1, null, _supply.Id);

            var vm = CreateVm();

            Assert.Equal(20, vm.RecentTransactions.Count);
        }

        [Fact]
        public void FilterCategory_restricts_recent_transactions_to_matching_inventory_items()
        {
            _inventoryService.ProcessTransaction(_paper.Id, 5, null, _supply.Id);
            _inventoryService.ProcessTransaction(_toner.Id, -1, _orderForToner.Id, _supply.Id);

            var vm = CreateVm();
            vm.FilterCategory = InventoryCategory.IT_Equipment;

            Assert.Single(vm.RecentTransactions);
            Assert.Equal(_toner.Id, vm.RecentTransactions[0].InventoryItemId);
        }

        [Fact]
        public void FilterInitiator_restricts_recent_transactions_to_selected_employee()
        {
            _inventoryService.ProcessTransaction(_paper.Id, 5, null, _supply.Id);
            _inventoryService.ProcessTransaction(_toner.Id, -1, _orderForToner.Id, _ito.Id);

            var vm = CreateVm();

            // InitiatorOptions включает только реальных авторов журнала.
            Assert.Equal(2, vm.InitiatorOptions.Count);

            vm.FilterInitiator = vm.InitiatorOptions.Single(e => e.Id == _ito.Id);

            Assert.Single(vm.RecentTransactions);
            Assert.Equal(_ito.Id, vm.RecentTransactions[0].InitiatorId);
        }

        [Fact]
        public void FilterFrom_and_FilterTo_restrict_recent_transactions_to_inclusive_date_range()
        {
            // Не используем сервис, чтобы выставить произвольные TransactionDate.
            _inventory.RecordTransaction(new InventoryTransaction
            {
                InventoryItemId = _paper.Id,
                QuantityChanged = 1,
                InitiatorId = _supply.Id,
                TransactionDate = new DateTime(2026, 1, 5, 12, 0, 0)
            });
            _inventory.RecordTransaction(new InventoryTransaction
            {
                InventoryItemId = _paper.Id,
                QuantityChanged = 1,
                InitiatorId = _supply.Id,
                TransactionDate = new DateTime(2026, 2, 10, 9, 0, 0)
            });
            _inventory.RecordTransaction(new InventoryTransaction
            {
                InventoryItemId = _paper.Id,
                QuantityChanged = 1,
                InitiatorId = _supply.Id,
                TransactionDate = new DateTime(2026, 3, 15, 18, 0, 0)
            });

            var vm = CreateVm();
            vm.FilterFrom = new DateTime(2026, 2, 1);
            vm.FilterTo = new DateTime(2026, 2, 28);

            Assert.Single(vm.RecentTransactions);
            Assert.Equal(new DateTime(2026, 2, 10, 9, 0, 0),
                         vm.RecentTransactions[0].TransactionDate);
        }

        [Fact]
        public void FilterCategory_works_when_InventoryItem_navigation_is_null()
        {
            // Bug #5 (review). Регрессия: фильтр по категории должен срабатывать
            // даже когда t.InventoryItem == null (в проде это случается, если
            // EF6 .Include() не отработал или данные пришли из стороннего
            // источника). Используем стаб-репозиторий, который на каждом
            // ListTransactions() возвращает свежие копии InventoryTransaction
            // с null-навигациями — это эмулирует EF без Include.
            var stubInv = new StubInventoryRepository();
            stubInv.AddItem(new InventoryItem
            {
                Id = 10,
                Name = "Расходник IT",
                Category = InventoryCategory.IT_Equipment,
                Unit = "шт.",
                TotalQuantity = 5
            });
            stubInv.AddItem(new InventoryItem
            {
                Id = 11,
                Name = "Канцелярия",
                Category = InventoryCategory.Stationery,
                Unit = "шт.",
                TotalQuantity = 5
            });
            stubInv.RecordTransaction(new InventoryTransaction
            {
                InventoryItemId = 10,
                QuantityChanged = 1,
                InitiatorId = _supply.Id,
                TransactionDate = DateTime.Now
            });
            stubInv.RecordTransaction(new InventoryTransaction
            {
                InventoryItemId = 11,
                QuantityChanged = 1,
                InitiatorId = _supply.Id,
                TransactionDate = DateTime.Now.AddSeconds(1)
            });

            var vm = new WarehouseViewModel(stubInv, new InventoryService(stubInv),
                                            _documents, _auth, _reports, _fileDialog, _employees);
            vm.FilterCategory = InventoryCategory.IT_Equipment;

            // Без фикса фильтр выкидывал ВСЕ транзакции (т.к. t.InventoryItem
            // был null до foreach-патчинга). С фиксом — остаётся ровно одна
            // транзакция с InventoryItemId == 10.
            Assert.Single(vm.RecentTransactions);
            Assert.Equal(10, vm.RecentTransactions[0].InventoryItemId);
        }

        [Fact]
        public void ClearFiltersCommand_resets_all_filters_and_repopulates_full_journal()
        {
            _inventoryService.ProcessTransaction(_paper.Id, 5, null, _supply.Id);
            _inventoryService.ProcessTransaction(_toner.Id, -1, _orderForToner.Id, _ito.Id);

            var vm = CreateVm();
            vm.FilterCategory = InventoryCategory.Stationery;
            vm.FilterInitiator = vm.InitiatorOptions.First(e => e.Id == _supply.Id);
            Assert.Single(vm.RecentTransactions);

            vm.ClearFiltersCommand.Execute(null);

            Assert.Null(vm.FilterCategory);
            Assert.Null(vm.FilterInitiator);
            Assert.Null(vm.FilterFrom);
            Assert.Null(vm.FilterTo);
            Assert.Equal(2, vm.RecentTransactions.Count);
        }

        // ---------------- Stub-зависимости ----------------

        private sealed class StubEmployeeRepository : IEmployeeRepository
        {
            private readonly List<Employee> _items = new List<Employee>();
            public void Add(Employee e) => _items.Add(e);
            public Employee FindByFullName(string fullName)
                => _items.FirstOrDefault(e => string.Equals(e.FullName, fullName, StringComparison.Ordinal));
            public Employee GetById(int id) => _items.FirstOrDefault(e => e.Id == id);
            public IReadOnlyList<Employee> ListAll() => _items.AsReadOnly();
        }

        private sealed class StubAuthService : IAuthService
        {
            public Employee CurrentEmployee { get; set; }
            public bool IsAuthenticated => CurrentEmployee != null;
            public LoginFailureReason LastFailureReason => LoginFailureReason.None;
            public bool TryLogin(string fullName, string password) => false;
            public void Logout() => CurrentEmployee = null;
        }

        private sealed class StubReportService : IReportService
        {
            public void ExportInventoryToExcel(string filePath) { }
            public void GenerateArchiveCertificate(int archiveRequestId, string filePath) { }
            public void ExportRegistrationJournal(IEnumerable<Document> documents, string title, string filePath) { }
            public void ExportExecutionDisciplineReport(DateTime from, DateTime to, string filePath) { }
            public void ExportDocumentVolumeReport(DateTime from, DateTime to, string filePath) { }
            public void ExportOverdueTasksReport(string filePath) { }
            public void ExportNomenclatureAnalyticsReport(DateTime from, DateTime to, string filePath) { }
            public void ExportOutgoingDispatchRegistry(DateTime from, DateTime to, string filePath) { }
            public void GenerateCaseInventory(int nomenclatureCaseId, string filePath) { }
            public void ExportFleetReport(DateTime from, DateTime to, string filePath) { }
            public void ExportInventoryTurnoverReport(DateTime from, DateTime to, string filePath) { }
            public void ExportDocumentAuditTrail(int documentId, string filePath) { }
        }

        private sealed class StubFileDialogService : IFileDialogService
        {
            public string PromptSaveFile(string title, string filter, string defaultFileName) => null;
            public string PromptOpenFile(string title, string filter) => null;
        }

        /// <summary>
        /// Стаб-репозиторий, эмулирующий поведение EF6 без правильно
        /// настроенного <c>.Include()</c>: каждая <c>ListTransactions()</c>
        /// возвращает СВЕЖИЕ копии <see cref="InventoryTransaction"/> с
        /// null-навигациями. Это вскрывает фильтры, которые ошибочно
        /// полагаются на <c>t.InventoryItem != null</c>.
        /// </summary>
        private sealed class StubInventoryRepository : IInventoryRepository
        {
            private readonly List<InventoryItem> _items = new List<InventoryItem>();
            private readonly List<InventoryTransaction> _transactions = new List<InventoryTransaction>();
            private int _nextItemId = 1;
            private int _nextTxId = 1;

            public IReadOnlyList<InventoryItem> ListItems() => _items.ToList();

            public InventoryItem GetItem(int itemId) =>
                _items.FirstOrDefault(i => i.Id == itemId);

            public IReadOnlyList<InventoryTransaction> ListTransactions(int? itemId = null)
            {
                IEnumerable<InventoryTransaction> q = _transactions;
                if (itemId.HasValue) q = q.Where(t => t.InventoryItemId == itemId.Value);
                return q.OrderByDescending(t => t.TransactionDate)
                        .Select(t => new InventoryTransaction
                        {
                            Id = t.Id,
                            InventoryItemId = t.InventoryItemId,
                            QuantityChanged = t.QuantityChanged,
                            DocumentId = t.DocumentId,
                            BasisDocumentId = t.BasisDocumentId,
                            InitiatorId = t.InitiatorId,
                            TransactionDate = t.TransactionDate
                        })
                        .ToList()
                        .AsReadOnly();
            }

            public void AddItem(InventoryItem item)
            {
                if (item.Id == 0) item.Id = _nextItemId++;
                else _nextItemId = Math.Max(_nextItemId, item.Id + 1);
                _items.Add(item);
            }

            public void RecordTransaction(InventoryTransaction transaction)
            {
                if (transaction.Id == 0) transaction.Id = _nextTxId++;
                else _nextTxId = Math.Max(_nextTxId, transaction.Id + 1);
                _transactions.Add(transaction);
            }
        }
    }
}
