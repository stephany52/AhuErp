using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Поведение <see cref="ApprovalService"/>: запуск маршрута,
    /// применение решений, корректное завершение маршрута.
    /// </summary>
    public class ApprovalServiceTests
    {
        private readonly InMemoryDocumentRepository _docs = new InMemoryDocumentRepository();
        private readonly InMemoryApprovalRepository _approvalRepo = new InMemoryApprovalRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly AuditService _audit;
        private readonly ApprovalService _service;
        private readonly Document _doc;
        private readonly ApprovalRouteTemplate _template;

        public ApprovalServiceTests()
        {
            _audit = new AuditService(_auditRepo);
            _service = new ApprovalService(_approvalRepo, _docs, _audit);

            _doc = new Document
            {
                Title = "Договор",
                Type = DocumentType.Office,
                CreationDate = DateTime.Now,
                Deadline = DateTime.Now.AddDays(7)
            };
            _docs.Add(_doc);

            _template = _service.AddTemplate(new ApprovalRouteTemplate
            {
                Name = "Согласование договора",
                IsActive = true
            });
            _service.AddStage(_template.Id, new ApprovalStage
            {
                Order = 1, ApproverEmployeeId = 10, Description = "Юрист"
            });
            _service.AddStage(_template.Id, new ApprovalStage
            {
                Order = 2, ApproverEmployeeId = 20, Description = "Главбух"
            });
        }

        [Fact]
        public void StartApproval_creates_approvals_per_stage()
        {
            var approvals = _service.StartApproval(_doc.Id, _template.Id, actorId: 1);

            Assert.Equal(2, approvals.Count);
            Assert.All(approvals, a => Assert.Equal(ApprovalDecision.Pending, a.Decision));
            Assert.Equal(ApprovalRouteStatus.InProgress, _docs.GetById(_doc.Id).ApprovalStatus);
        }

        [Fact]
        public void ApplyDecision_completes_route_when_all_approved()
        {
            var approvals = _service.StartApproval(_doc.Id, _template.Id, actorId: 1);
            _service.ApplyDecision(approvals[0].Id, ApprovalDecision.Approved, actorId: 10);
            var second = _service.ApplyDecision(approvals[1].Id, ApprovalDecision.Approved, actorId: 20);

            Assert.Equal(ApprovalDecision.Approved, second.Decision);
            Assert.Equal(ApprovalRouteStatus.Completed, _docs.GetById(_doc.Id).ApprovalStatus);
        }

        [Fact]
        public void ApplyDecision_rejected_marks_route_rejected()
        {
            var approvals = _service.StartApproval(_doc.Id, _template.Id, actorId: 1);
            _service.ApplyDecision(approvals[0].Id, ApprovalDecision.Rejected, actorId: 10, comment: "Текст недопустим");

            Assert.Equal(ApprovalRouteStatus.Rejected, _docs.GetById(_doc.Id).ApprovalStatus);
        }

        [Fact]
        public void ApplyDecision_throws_when_already_decided()
        {
            var approvals = _service.StartApproval(_doc.Id, _template.Id, actorId: 1);
            _service.ApplyDecision(approvals[0].Id, ApprovalDecision.Approved, actorId: 10);
            Assert.Throws<InvalidOperationException>(() =>
                _service.ApplyDecision(approvals[0].Id, ApprovalDecision.Rejected, actorId: 10));
        }

        [Fact]
        public void StartApproval_throws_when_template_has_no_stages()
        {
            var emptyTpl = _service.AddTemplate(new ApprovalRouteTemplate { Name = "Пустой", IsActive = true });
            Assert.Throws<InvalidOperationException>(() =>
                _service.StartApproval(_doc.Id, emptyTpl.Id, 1));
        }

        [Fact]
        public void StartApproval_throws_when_stage_has_no_approver()
        {
            var tpl = _service.AddTemplate(new ApprovalRouteTemplate { Name = "Без согласующего", IsActive = true });
            _service.AddStage(tpl.Id, new ApprovalStage { Order = 1, ApproverEmployeeId = null });
            Assert.Throws<InvalidOperationException>(() =>
                _service.StartApproval(_doc.Id, tpl.Id, 1));
        }

        [Fact]
        public void Comments_decision_does_not_complete_route_alone()
        {
            var approvals = _service.StartApproval(_doc.Id, _template.Id, actorId: 1);
            _service.ApplyDecision(approvals[0].Id, ApprovalDecision.Comments, actorId: 10, comment: "Уточнить");

            // Один этап на Comments + второй ещё в Pending → маршрут не завершён.
            Assert.Equal(ApprovalRouteStatus.InProgress, _docs.GetById(_doc.Id).ApprovalStatus);
        }

        // ============================================================
        // Bug #6 — авторизация ApplyDecision строго по Stage.ApproverId,
        // никаких проверок по EmployeeRole. Сотрудник с любой ролью
        // (включая TechSupport) обязан мочь согласовать «свой» этап.
        // ============================================================

        [Fact]
        public void ApplyDecision_allows_owner_independent_of_role()
        {
            // Этап выписан на сотрудника #10 (роль не задана). Сервис не
            // должен запрашивать роль вообще — достаточно совпадения Id.
            var approvals = _service.StartApproval(_doc.Id, _template.Id, actorId: 1);
            var first = approvals[0];

            var result = _service.ApplyDecision(first.Id, ApprovalDecision.Approved, actorId: 10);

            Assert.Equal(ApprovalDecision.Approved, result.Decision);
        }

        [Fact]
        public void ApplyDecision_throws_for_unrelated_actor()
        {
            // Сотрудник #999 не согласующий и не имеет замещения.
            var approvals = _service.StartApproval(_doc.Id, _template.Id, actorId: 1);

            var ex = Assert.Throws<UnauthorizedAccessException>(() =>
                _service.ApplyDecision(approvals[0].Id, ApprovalDecision.Approved, actorId: 999));
            Assert.Contains("не является согласующим", ex.Message);
        }

        [Fact]
        public void ApplyDecision_allows_active_substitute_via_substitution_service()
        {
            // Сценарий: согласующий #10 уехал, на ApprovalsOnly активна
            // подмена на #50. На момент StartApproval substitution-сервис
            // уже резолвнул этап на #50 — это и есть «авторизация по факту».
            var subs = new InMemorySubstitutionRepository();
            var subAudit = new AuditService(new InMemoryAuditLogRepository());
            var subSvc = new SubstitutionService(subs, subAudit);
            subSvc.Create(originalId: 10, substituteId: 50,
                from: DateTime.Now.AddDays(-1), to: DateTime.Now.AddDays(1),
                scope: SubstitutionScope.ApprovalsOnly, reason: "отпуск", actorId: 1);

            var service = new ApprovalService(
                _approvalRepo, _docs, _audit,
                workflow: null, signatures: null,
                substitution: subSvc, notifications: null);

            var approvals = service.StartApproval(_doc.Id, _template.Id, actorId: 1);
            // Этап #1 был выписан на #10, но фактический владелец после
            // резолва замещения — #50. Подменяемый исходный сотрудник
            // (#10) уже не должен мочь применить решение.
            Assert.Equal(50, approvals[0].ApproverId);

            var result = service.ApplyDecision(approvals[0].Id, ApprovalDecision.Approved, actorId: 50);
            Assert.Equal(ApprovalDecision.Approved, result.Decision);
        }

        [Fact]
        public void ApplyDecision_does_not_check_employee_role()
        {
            // Регрессионный тест Bug #6: сотрудник с ролью TechSupport,
            // если он назначен согласующим этапа, должен иметь право
            // согласования. Никакой проверки EmployeeRole внутри
            // ApprovalService быть не должно.
            var approvals = _service.StartApproval(_doc.Id, _template.Id, actorId: 1);
            var first = approvals[0]; // ApproverId == 10

            // Любая роль, лишь бы Id совпадал — должно сработать.
            var result = _service.ApplyDecision(first.Id, ApprovalDecision.Approved, actorId: 10);

            Assert.Equal(ApprovalDecision.Approved, result.Decision);
        }
    }
}
