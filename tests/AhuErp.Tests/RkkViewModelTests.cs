using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using AhuErp.UI.ViewModels;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Bug #3 — поведение РКК для свежесозданного документа. До исправления
    /// баннер «Документ заблокирован КЭП» иногда оказывался виден на пустой
    /// карточке, потому что биндинг шёл напрямую на <c>SelectedDocument.IsLocked</c>
    /// и не учитывал отсутствие квалифицированной подписи. Теперь
    /// <see cref="RkkViewModel.IsDocumentLocked"/> проверяет оба условия,
    /// а <see cref="RkkViewModel.New"/> явно создаёт шаблон-черновик с
    /// IsLocked=false.
    /// </summary>
    public class RkkViewModelTests
    {
        private readonly InMemoryDocumentRepository _docs = new InMemoryDocumentRepository();
        private readonly InMemoryAttachmentRepository _attRepo = new InMemoryAttachmentRepository();
        private readonly InMemoryFileStorageService _storage = new InMemoryFileStorageService();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly InMemorySignatureRepository _sigRepo = new InMemorySignatureRepository();
        private readonly InMemoryEmployeeRepository _employees;
        private readonly InMemoryNomenclatureRepository _nomenRepo = new InMemoryNomenclatureRepository();
        private readonly InMemoryTaskRepository _tasksRepo = new InMemoryTaskRepository();
        private readonly InMemoryApprovalRepository _approvalRepo = new InMemoryApprovalRepository();
        private readonly InMemoryInventoryRepository _inventoryRepo = new InMemoryInventoryRepository();
        private readonly InMemoryVehicleRepository _vehicleRepo = new InMemoryVehicleRepository();
        private readonly AuditService _audit;
        private readonly NomenclatureService _nomenclature;
        private readonly SignatureService _signatures;
        private readonly AttachmentService _attachments;
        private readonly TaskService _tasks;
        private readonly ApprovalService _approvals;
        private readonly InventoryService _inventory;
        private readonly FleetService _fleet;
        private readonly StubAuthService _auth;
        private readonly Employee _admin;
        private readonly Employee _author;

        public RkkViewModelTests()
        {
            _admin = new Employee { Id = 1, FullName = "Иванов И.И.", Role = EmployeeRole.Admin, PasswordHash = "h" };
            _author = new Employee { Id = 2, FullName = "Петров П.П.", Role = EmployeeRole.Manager, PasswordHash = "h" };
            _employees = new InMemoryEmployeeRepository(new[] { _admin, _author });
            _audit = new AuditService(_auditRepo);
            _nomenclature = new NomenclatureService(_nomenRepo, _docs, _audit);
            var hmac = new HmacCryptoProvider();
            _signatures = new SignatureService(_sigRepo, _docs, _attRepo, _employees, _audit,
                hmac: hmac, qualified: hmac);
            _attachments = new AttachmentService(_attRepo, _docs, _storage, _audit, _signatures);
            _tasks = new TaskService(_tasksRepo, _docs, _audit);
            _approvals = new ApprovalService(_approvalRepo, _docs, _audit);
            _inventory = new InventoryService(_inventoryRepo);
            _fleet = new FleetService(_vehicleRepo);
            _auth = new StubAuthService(_admin);
        }

        private RkkViewModel BuildVm()
            => new RkkViewModel(
                _docs, _nomenclature, _attachments, _tasks, _approvals, _audit,
                _auth, _inventory, _inventoryRepo, _fleet, _vehicleRepo,
                signatures: _signatures, employeeRepo: _employees, fileDialog: null);

        // ---------------- Bug #3: New() создаёт разблокированный черновик ----

        [Fact]
        public void New_CreatesUnlockedDraft()
        {
            var vm = BuildVm();
            vm.NewCommand.Execute(null);

            Assert.NotNull(vm.DraftDocumentTemplate);
            Assert.False(vm.DraftDocumentTemplate.IsLocked);
            Assert.Equal(DocumentStatus.New, vm.DraftDocumentTemplate.Status);
            Assert.Equal(ApprovalRouteStatus.Draft, vm.DraftDocumentTemplate.ApprovalStatus);
            Assert.Equal(DocumentAccessLevel.Public, vm.DraftDocumentTemplate.AccessLevel);
            Assert.Null(vm.SelectedDocument);
            Assert.False(vm.IsDocumentLocked);
        }

        // ---------------- Bug #3: IsDocumentLocked корректно учитывает оба условия

        [Fact]
        public void IsDocumentLocked_IsFalse_WhenSelectedDocumentIsNull()
        {
            var vm = BuildVm();
            vm.SelectedDocument = null;
            Assert.False(vm.IsDocumentLocked);
        }

        [Fact]
        public void IsDocumentLocked_IsFalse_WhenDocumentIsLockedButHasNoQualifiedSignature()
        {
            // Симулируем «легаси» документ с IsLocked=true, у которого по какой-то
            // причине нет активной Qualified-подписи (например, она отозвана).
            // Баннер всё равно не должен зажигаться — мы не запираем РКК просто
            // потому, что у неё ошибочно выставлен флаг.
            var doc = new Document
            {
                Title = "x",
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Public,
                CreationDate = DateTime.Now,
                AuthorId = _author.Id,
                IsLocked = true,
            };
            _docs.Add(doc);

            var vm = BuildVm();
            vm.SelectedDocument = doc;

            Assert.False(vm.IsDocumentLocked);
        }

        [Fact]
        public void IsDocumentLocked_IsTrue_WhenLockedAndQualifiedSignatureExists()
        {
            var doc = new Document
            {
                Title = "x",
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Public,
                CreationDate = DateTime.Now,
                AuthorId = _author.Id,
            };
            _docs.Add(doc);
            // Qualified-подпись сама поднимает IsLocked=true внутри SignatureService.
            _signatures.Sign(doc.Id, attachmentId: null, signerId: _admin.Id,
                kind: SignatureKind.Qualified, certificateThumbprint: "CERT");

            var vm = BuildVm();
            vm.SelectedDocument = _docs.GetById(doc.Id);

            Assert.True(vm.SelectedDocument.IsLocked);
            Assert.True(vm.IsDocumentLocked);
        }

        // ---------------- Bug #3: «Снять блокировку» только Admin/Manager + AuditLog

        [Fact]
        public void UnlockDocument_AsAdmin_ClearsIsLockedAndWritesAudit()
        {
            var doc = new Document
            {
                Title = "x",
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Public,
                CreationDate = DateTime.Now,
                AuthorId = _author.Id,
            };
            _docs.Add(doc);
            _signatures.Sign(doc.Id, attachmentId: null, signerId: _admin.Id,
                kind: SignatureKind.Qualified, certificateThumbprint: "CERT");

            var vm = BuildVm();
            vm.SelectedDocument = _docs.GetById(doc.Id);
            Assert.True(vm.IsDocumentLocked);

            vm.UnlockDocumentCommand.Execute(null);

            var stored = _docs.GetById(doc.Id);
            Assert.False(stored.IsLocked);
            Assert.False(vm.IsDocumentLocked);
            // Запись в AuditLog с типом DocumentUnlocked (Bug #3 контракт).
            var unlocks = _auditRepo.Query(new AuditQueryFilter
            {
                ActionType = AuditActionType.DocumentUnlocked,
            });
            Assert.Single(unlocks);
        }

        [Fact]
        public void UnlockDocument_AsRegularEmployee_IsNotAllowed()
        {
            var doc = new Document
            {
                Title = "x",
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Public,
                CreationDate = DateTime.Now,
                AuthorId = _author.Id,
            };
            _docs.Add(doc);
            _signatures.Sign(doc.Id, attachmentId: null, signerId: _admin.Id,
                kind: SignatureKind.Qualified, certificateThumbprint: "CERT");

            // Прячем «обычного» сотрудника (без прав Admin/Manager) под текущей сессией.
            // Берём роль архивиста — единственная «не-управленческая» из перечисления EmployeeRole,
            // которая по контракту Bug #3 НЕ должна получать кнопку «Снять блокировку».
            var clerk = new Employee { Id = 3, FullName = "Сидоров С.С.", Role = EmployeeRole.Archivist, PasswordHash = "h" };
            _employees.Add(clerk);
            var auth = new StubAuthService(clerk);
            var vm = new RkkViewModel(
                _docs, _nomenclature, _attachments, _tasks, _approvals, _audit,
                auth, _inventory, _inventoryRepo, _fleet, _vehicleRepo,
                signatures: _signatures, employeeRepo: _employees, fileDialog: null);
            vm.SelectedDocument = _docs.GetById(doc.Id);

            Assert.False(vm.UnlockDocumentCommand.CanExecute(null));
        }

        // ---------------- helpers ----------------------------------------

        private sealed class StubAuthService : IAuthService
        {
            public StubAuthService(Employee current) { CurrentEmployee = current; }
            public Employee CurrentEmployee { get; }
            public bool IsAuthenticated => CurrentEmployee != null;
            public LoginFailureReason LastFailureReason => LoginFailureReason.None;
            public bool TryLogin(string fullName, string password) => false;
            public void Logout() { }
        }
    }
}
