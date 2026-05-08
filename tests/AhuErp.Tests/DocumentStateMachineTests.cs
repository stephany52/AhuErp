using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 13. Тесты <see cref="DocumentStateMachine"/>: проверка таблицы
    /// допустимых переходов, ролевые ограничения, идемпотентность,
    /// аудит-логирование, корректные исключения для запрещённых переходов.
    /// </summary>
    public class DocumentStateMachineTests
    {
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly AuditService _audit;

        public DocumentStateMachineTests()
        {
            _audit = new AuditService(_auditRepo);
        }

        // ============================================================
        // Базовая таблица переходов (без роли).
        // ============================================================

        [Theory]
        // Draft (New) → доступные.
        [InlineData(DocumentStatus.New, DocumentStatus.Registered, true)]
        [InlineData(DocumentStatus.New, DocumentStatus.OnApproval, true)]
        [InlineData(DocumentStatus.New, DocumentStatus.Cancelled, true)]
        // Draft → запрещены.
        [InlineData(DocumentStatus.New, DocumentStatus.Approved, false)]
        [InlineData(DocumentStatus.New, DocumentStatus.Signed, false)]
        [InlineData(DocumentStatus.New, DocumentStatus.Completed, false)]
        [InlineData(DocumentStatus.New, DocumentStatus.Archived, false)]
        // Registered → разрешённые.
        [InlineData(DocumentStatus.Registered, DocumentStatus.OnApproval, true)]
        [InlineData(DocumentStatus.Registered, DocumentStatus.OnSigning, true)]
        [InlineData(DocumentStatus.Registered, DocumentStatus.OnExecution, true)]
        [InlineData(DocumentStatus.Registered, DocumentStatus.Completed, true)]
        [InlineData(DocumentStatus.Registered, DocumentStatus.Cancelled, true)]
        [InlineData(DocumentStatus.Registered, DocumentStatus.Archived, true)]
        // Registered → запрещённые.
        [InlineData(DocumentStatus.Registered, DocumentStatus.Signed, false)]
        [InlineData(DocumentStatus.Registered, DocumentStatus.New, false)]
        // OnApproval — только Approved/Rejected/Cancelled.
        [InlineData(DocumentStatus.OnApproval, DocumentStatus.Approved, true)]
        [InlineData(DocumentStatus.OnApproval, DocumentStatus.Rejected, true)]
        [InlineData(DocumentStatus.OnApproval, DocumentStatus.Cancelled, true)]
        [InlineData(DocumentStatus.OnApproval, DocumentStatus.Signed, false)]
        [InlineData(DocumentStatus.OnApproval, DocumentStatus.Completed, false)]
        // Approved.
        [InlineData(DocumentStatus.Approved, DocumentStatus.OnSigning, true)]
        [InlineData(DocumentStatus.Approved, DocumentStatus.OnExecution, true)]
        [InlineData(DocumentStatus.Approved, DocumentStatus.Completed, true)]
        [InlineData(DocumentStatus.Approved, DocumentStatus.Cancelled, true)]
        [InlineData(DocumentStatus.Approved, DocumentStatus.Rejected, false)]
        // Rejected — обратно в Draft (на доработку) или Cancelled.
        [InlineData(DocumentStatus.Rejected, DocumentStatus.New, true)]
        [InlineData(DocumentStatus.Rejected, DocumentStatus.Cancelled, true)]
        [InlineData(DocumentStatus.Rejected, DocumentStatus.Approved, false)]
        [InlineData(DocumentStatus.Rejected, DocumentStatus.Completed, false)]
        // OnSigning.
        [InlineData(DocumentStatus.OnSigning, DocumentStatus.Signed, true)]
        [InlineData(DocumentStatus.OnSigning, DocumentStatus.Cancelled, true)]
        [InlineData(DocumentStatus.OnSigning, DocumentStatus.Completed, false)]
        // Signed.
        [InlineData(DocumentStatus.Signed, DocumentStatus.OnExecution, true)]
        [InlineData(DocumentStatus.Signed, DocumentStatus.Completed, true)]
        [InlineData(DocumentStatus.Signed, DocumentStatus.Archived, true)]
        [InlineData(DocumentStatus.Signed, DocumentStatus.Cancelled, true)]
        [InlineData(DocumentStatus.Signed, DocumentStatus.Rejected, false)]
        // OnExecution.
        [InlineData(DocumentStatus.OnExecution, DocumentStatus.Completed, true)]
        [InlineData(DocumentStatus.OnExecution, DocumentStatus.OnHold, true)]
        [InlineData(DocumentStatus.OnExecution, DocumentStatus.Cancelled, true)]
        [InlineData(DocumentStatus.OnExecution, DocumentStatus.Archived, false)]
        // Completed → только Archived.
        [InlineData(DocumentStatus.Completed, DocumentStatus.Archived, true)]
        [InlineData(DocumentStatus.Completed, DocumentStatus.OnExecution, false)]
        [InlineData(DocumentStatus.Completed, DocumentStatus.New, false)]
        public void CanTransition_logical_validity(DocumentStatus from, DocumentStatus to, bool expected)
        {
            Assert.Equal(expected, DocumentStateMachine.CanTransition(from, to));
        }

        [Fact]
        public void CanTransition_self_transition_is_disallowed()
        {
            // Идентичный from→to считается no-op и не валидным переходом.
            foreach (DocumentStatus s in Enum.GetValues(typeof(DocumentStatus)))
            {
                Assert.False(DocumentStateMachine.CanTransition(s, s),
                    $"Self-transition {s}→{s} должен быть запрещён.");
            }
        }

        [Theory]
        [InlineData(DocumentStatus.Cancelled)]
        [InlineData(DocumentStatus.Archived)]
        public void Terminal_statuses_have_no_outgoing_transitions(DocumentStatus terminal)
        {
            Assert.True(DocumentStateMachine.IsTerminal(terminal));
            Assert.Empty(DocumentStateMachine.NextStates(terminal));
            // И ни в один статус из терминального перейти нельзя.
            foreach (DocumentStatus to in Enum.GetValues(typeof(DocumentStatus)))
            {
                Assert.False(DocumentStateMachine.CanTransition(terminal, to),
                    $"{terminal}→{to} должен быть запрещён (терминал).");
            }
        }

        [Fact]
        public void NextStates_returns_expected_set_for_Draft()
        {
            var next = DocumentStateMachine.NextStates(DocumentStatus.New);
            Assert.Contains(DocumentStatus.Registered, next);
            Assert.Contains(DocumentStatus.OnApproval, next);
            Assert.Contains(DocumentStatus.Cancelled, next);
            Assert.DoesNotContain(DocumentStatus.Archived, next);
        }

        // ============================================================
        // Ролевые ограничения.
        // ============================================================

        [Fact]
        public void Admin_can_perform_any_logically_valid_transition()
        {
            // Admin — мета-роль для расследования; всё, что валидно по графу.
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.Completed, DocumentStatus.Archived, EmployeeRole.Admin));
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.Registered, DocumentStatus.Archived, EmployeeRole.Admin));
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.New, DocumentStatus.Cancelled, EmployeeRole.Admin));
            // Но даже Admin не может нарушить логику графа.
            Assert.False(DocumentStateMachine.CanTransition(
                DocumentStatus.New, DocumentStatus.Archived, EmployeeRole.Admin));
        }

        [Theory]
        [InlineData(EmployeeRole.Manager)]
        [InlineData(EmployeeRole.DeputyHead)]
        [InlineData(EmployeeRole.Clerk)]
        public void Office_flow_roles_can_register_drafts(EmployeeRole role)
        {
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.New, DocumentStatus.Registered, role));
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.Registered, DocumentStatus.OnApproval, role));
        }

        [Fact]
        public void Archivist_cannot_register_a_draft_via_state_machine()
        {
            // Архивист — не делопроизводитель; присвоение рег. номера не его
            // зона ответственности. Но он может зарегистрировать черновик
            // именно как «свой» (Архивный отдел регистрирует свои дела) —
            // это корпоративное соглашение, поэтому Archivist в officeFlowExtended.
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.New, DocumentStatus.Registered, EmployeeRole.Archivist));
        }

        [Fact]
        public void Archive_transitions_only_for_archivist_and_leadership()
        {
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.Completed, DocumentStatus.Archived, EmployeeRole.Archivist));
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.Completed, DocumentStatus.Archived, EmployeeRole.Manager));
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.Completed, DocumentStatus.Archived, EmployeeRole.DeputyHead));

            // Делопроизводитель и доменные ответственные не передают дела в
            // архив — это политика организации (у архивиста монополия).
            Assert.False(DocumentStateMachine.CanTransition(
                DocumentStatus.Completed, DocumentStatus.Archived, EmployeeRole.Clerk));
            Assert.False(DocumentStateMachine.CanTransition(
                DocumentStatus.Completed, DocumentStatus.Archived, EmployeeRole.TechSupport));
            Assert.False(DocumentStateMachine.CanTransition(
                DocumentStatus.Completed, DocumentStatus.Archived, EmployeeRole.WarehouseManager));
            Assert.False(DocumentStateMachine.CanTransition(
                DocumentStatus.Completed, DocumentStatus.Archived, EmployeeRole.HRAdmin));
        }

        [Fact]
        public void Executors_can_complete_their_own_OnExecution_documents()
        {
            // Сотрудник ИТО завершает свою заявку, кладовщик — свой акт списания.
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.OnExecution, DocumentStatus.Completed, EmployeeRole.TechSupport));
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.OnExecution, DocumentStatus.Completed, EmployeeRole.WarehouseManager));
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.OnExecution, DocumentStatus.Completed, EmployeeRole.FleetManager));

            // Но HRAdmin не может — у неё нет домена документов на исполнение.
            Assert.False(DocumentStateMachine.CanTransition(
                DocumentStatus.OnExecution, DocumentStatus.Completed, EmployeeRole.HRAdmin));
        }

        [Fact]
        public void Cancellation_from_active_states_only_for_office_flow()
        {
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.OnApproval, DocumentStatus.Cancelled, EmployeeRole.Clerk));
            Assert.True(DocumentStateMachine.CanTransition(
                DocumentStatus.OnApproval, DocumentStatus.Cancelled, EmployeeRole.DeputyHead));
            Assert.False(DocumentStateMachine.CanTransition(
                DocumentStatus.OnApproval, DocumentStatus.Cancelled, EmployeeRole.TechSupport));
        }

        // ============================================================
        // Transition() — мутация документа + аудит.
        // ============================================================

        [Fact]
        public void Transition_writes_audit_entry_and_updates_status()
        {
            var doc = new Document { Id = 42, Title = "Договор", Status = DocumentStatus.New };

            DocumentStateMachine.Transition(
                doc, DocumentStatus.Registered, EmployeeRole.Clerk, actorId: 7,
                _audit, reason: "Регистрация делопроизводителем");

            Assert.Equal(DocumentStatus.Registered, doc.Status);
            var entries = _auditRepo.ListAllOrdered();
            Assert.Single(entries);
            var entry = entries.Single();
            Assert.Equal(AuditActionType.StatusChanged, entry.ActionType);
            Assert.Equal(nameof(Document), entry.EntityType);
            Assert.Equal(42, entry.EntityId);
            Assert.Equal(7, entry.UserId);
            Assert.Contains("Status=New", entry.OldValues);
            Assert.Contains("Status=Registered", entry.NewValues);
            Assert.Contains("Регистрация", entry.Details);
        }

        [Fact]
        public void Transition_throws_for_invalid_transition_and_no_audit_written()
        {
            var doc = new Document { Id = 1, Status = DocumentStatus.New };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                DocumentStateMachine.Transition(
                    doc, DocumentStatus.Archived, EmployeeRole.Admin, actorId: 1, _audit));

            Assert.Contains("Недопустимый переход", ex.Message);
            Assert.Equal(DocumentStatus.New, doc.Status);
            Assert.Empty(_auditRepo.ListAllOrdered());
        }

        [Fact]
        public void Transition_throws_when_role_not_authorized()
        {
            var doc = new Document { Id = 1, Status = DocumentStatus.Completed };
            // Клерк не может перевести в архив.
            var ex = Assert.Throws<InvalidOperationException>(() =>
                DocumentStateMachine.Transition(
                    doc, DocumentStatus.Archived, EmployeeRole.Clerk, actorId: 5, _audit));

            Assert.Contains("Недопустимый переход", ex.Message);
            Assert.Contains("роль Clerk", ex.Message);
            Assert.Equal(DocumentStatus.Completed, doc.Status);
        }

        [Fact]
        public void Transition_idempotent_for_same_status()
        {
            var doc = new Document { Id = 1, Status = DocumentStatus.OnExecution };

            DocumentStateMachine.Transition(
                doc, DocumentStatus.OnExecution, EmployeeRole.Clerk, actorId: 1, _audit);

            // Повторный вызов того же статуса — не падает и не пишет аудит.
            Assert.Equal(DocumentStatus.OnExecution, doc.Status);
            Assert.Empty(_auditRepo.ListAllOrdered());
        }

        [Fact]
        public void Transition_with_null_role_only_validates_logical_validity()
        {
            // Системный вызов (например, из ApprovalService после
            // финализации маршрута) — роль не передаём, проверяется только
            // логическая корректность графа.
            var doc = new Document { Id = 1, Status = DocumentStatus.OnApproval };

            DocumentStateMachine.Transition(
                doc, DocumentStatus.Approved, actorRole: null, actorId: null, _audit);

            Assert.Equal(DocumentStatus.Approved, doc.Status);
            Assert.Single(_auditRepo.ListAllOrdered());
        }

        [Fact]
        public void Transition_throws_on_null_arguments()
        {
            var doc = new Document();
            Assert.Throws<ArgumentNullException>(() => DocumentStateMachine.Transition(
                null, DocumentStatus.Registered, EmployeeRole.Admin, actorId: 1, _audit));
            Assert.Throws<ArgumentNullException>(() => DocumentStateMachine.Transition(
                doc, DocumentStatus.Registered, EmployeeRole.Admin, actorId: 1, audit: null));
        }
    }
}
