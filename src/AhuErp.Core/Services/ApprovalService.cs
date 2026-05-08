using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IApprovalService"/>. Логика прохождения:
    /// <list type="bullet">
    ///   <item><description>Шаблон содержит этапы (<see cref="ApprovalStage"/>) с
    ///     порядком и флагом параллельности.</description></item>
    ///   <item><description>При запуске маршрута для документа создаются
    ///     <see cref="DocumentApproval"/> с тем же порядком.</description></item>
    ///   <item><description>Решение «Отклонено» останавливает маршрут;
    ///     все Approved → завершение маршрута.</description></item>
    /// </list>
    /// Все ключевые события идут в журнал аудита.
    /// </summary>
    public sealed class ApprovalService : IApprovalService
    {
        private readonly IApprovalRepository _repository;
        private readonly IDocumentRepository _documents;
        private readonly IAuditService _audit;
        private readonly IWorkflowService _workflow;
        private readonly ISignatureService _signatures;
        private readonly ISubstitutionService _substitution;
        private readonly INotificationService _notifications;

        public ApprovalService(
            IApprovalRepository repository,
            IDocumentRepository documents,
            IAuditService audit,
            IWorkflowService workflow = null,
            ISignatureService signatures = null,
            ISubstitutionService substitution = null,
            INotificationService notifications = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _workflow = workflow;
            _signatures = signatures;
            _substitution = substitution;
            _notifications = notifications;
        }

        public IReadOnlyList<ApprovalRouteTemplate> ListTemplates(bool activeOnly = true)
            => _repository.ListTemplates(activeOnly);

        public ApprovalRouteTemplate GetTemplate(int id) => _repository.GetTemplate(id);

        public ApprovalRouteTemplate AddTemplate(ApprovalRouteTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (string.IsNullOrWhiteSpace(template.Name))
                throw new ArgumentException("Имя шаблона обязательно.", nameof(template));
            return _repository.AddTemplate(template);
        }

        public ApprovalStage AddStage(int templateId, ApprovalStage stage)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            stage.RouteTemplateId = templateId;
            return _repository.AddStage(stage);
        }

        public IReadOnlyList<DocumentApproval> StartApproval(int documentId, int templateId, int actorId)
        {
            var doc = _documents.GetById(documentId)
                ?? throw new InvalidOperationException($"Документ #{documentId} не найден.");
            var template = _repository.GetTemplate(templateId)
                ?? throw new InvalidOperationException($"Шаблон маршрута #{templateId} не найден.");

            var stages = _repository.ListStages(templateId)
                .OrderBy(s => s.Order)
                .ToList();
            if (stages.Count == 0)
                throw new InvalidOperationException("В шаблоне маршрута нет этапов.");
            if (stages.Any(s => !s.ApproverEmployeeId.HasValue))
                throw new InvalidOperationException(
                    "Этапы шаблона должны иметь конкретного согласующего (ApproverEmployeeId).");

            var approvals = new List<DocumentApproval>();
            var now = DateTime.Now;
            foreach (var stage in stages)
            {
                // Phase 11: согласующего может замещать другой сотрудник.
                int actualApproverId = stage.ApproverEmployeeId.Value;
                if (_substitution != null)
                {
                    actualApproverId = _substitution.ResolveActualExecutor(
                        actualApproverId, now, SubstitutionScope.ApprovalsOnly);
                }

                var approval = _repository.AddApproval(new DocumentApproval
                {
                    DocumentId = doc.Id,
                    StageId = stage.Id,
                    Order = stage.Order,
                    IsParallel = stage.IsParallel,
                    ApproverId = actualApproverId,
                    Decision = ApprovalDecision.Pending
                });
                approvals.Add(approval);
            }

            doc.ApprovalStatus = ApprovalRouteStatus.InProgress;
            _documents.Update(doc);

            _audit.Record(AuditActionType.ApprovalSent, nameof(Document), doc.Id, actorId,
                newValues: $"TemplateId={templateId}; Stages={stages.Count}");

            // Phase 9: уведомление согласующим о необходимости рассмотрения.
            // Параллельные этапы получают уведомление сразу, последовательные —
            // на каждом этапе отдельно (упрощение: всем сразу — TickReminders
            // позаботится о повторных напоминаниях о дедлайнах).
            if (_notifications != null)
            {
                foreach (var a in approvals)
                {
                    _notifications.Create(a.ApproverId, NotificationKind.ApprovalRequired,
                        $"Документ на согласование (#{doc.Id})",
                        $"Маршрут «{template.Name}», этап #{a.Order}. Документ: {doc.Title}",
                        docId: doc.Id, approvalId: a.Id);
                }
            }

            return approvals.AsReadOnly();
        }

        public DocumentApproval ApplyDecision(int approvalId, ApprovalDecision decision, int actorId, string comment = null)
        {
            if (decision == ApprovalDecision.Pending)
                throw new ArgumentException("Решение Pending не может быть применено вручную.");

            var approval = _repository.GetApproval(approvalId)
                ?? throw new InvalidOperationException($"Этап согласования #{approvalId} не найден.");
            if (approval.Decision != ApprovalDecision.Pending)
                throw new InvalidOperationException("По этому этапу уже принято решение.");

            // Bug #6: авторизация на уровне согласования — это владелец этапа
            // (ApproverId, который сам уже резолвлен через активное замещение
            // на момент StartApproval) ИЛИ сотрудник, для которого активно
            // замещение того, кто исходно владеет этапом. Никаких проверок по
            // EmployeeRole — иначе TechSupport/любая роль не сможет согласовать
            // свой собственный этап.
            if (!IsAuthorizedActor(approval, actorId))
                throw new UnauthorizedAccessException(
                    $"Сотрудник #{actorId} не является согласующим этапа #{approval.Id} " +
                    "и не имеет активного замещения на эту область.");

            approval.Decision = decision;
            approval.Comment = comment;
            approval.DecisionDate = DateTime.UtcNow;
            _repository.UpdateApproval(approval);

            _audit.Record(
                decision == ApprovalDecision.Approved ? AuditActionType.ApprovalSigned
                    : decision == ApprovalDecision.Rejected ? AuditActionType.ApprovalRejected
                    : AuditActionType.Updated,
                nameof(DocumentApproval), approval.Id, actorId,
                newValues: $"Decision={decision}", details: comment);

            // Завершаем маршрут целиком, если: либо отклонено, либо все этапы
            // приняты. Замечания не блокируют маршрут — это «мягкое» решение.
            var all = _repository.ListApprovalsByDocument(approval.DocumentId);
            var doc = _documents.GetById(approval.DocumentId);
            if (decision == ApprovalDecision.Rejected)
            {
                doc.ApprovalStatus = ApprovalRouteStatus.Rejected;
                _documents.Update(doc);
            }
            else if (all.All(a => a.Decision == ApprovalDecision.Approved
                                   || a.Decision == ApprovalDecision.Comments))
            {
                doc.ApprovalStatus = ApprovalRouteStatus.Completed;
                _documents.Update(doc);
                _workflow?.OnApprovalRouteCompleted(doc.Id, actorId);

                // Phase 8 — последний этап одобрен → автоматически ставим
                // ПЭП от имени системы (от того, кто принял финальное решение).
                if (_signatures != null)
                {
                    try
                    {
                        _signatures.Sign(doc.Id, attachmentId: null, signerId: actorId,
                            kind: SignatureKind.Simple,
                            reason: $"Согласовано по маршруту, этап #{approval.Id}");
                    }
                    catch (InvalidOperationException)
                    {
                        // Подпись уже стояла или сотрудник недоступен — не валим бизнес-операцию.
                    }
                }
            }

            // Phase 9: уведомить автора документа о принятом решении.
            if (_notifications != null && doc.AuthorId.HasValue)
            {
                _notifications.Create(doc.AuthorId.Value, NotificationKind.ApprovalDecided,
                    $"Решение по согласованию (#{approval.Id})",
                    $"Документ «{doc.Title}», этап #{approval.Order}: {decision}.",
                    docId: doc.Id, approvalId: approval.Id);
            }

            return approval;
        }

        public IReadOnlyList<DocumentApproval> ListByDocument(int documentId)
            => _repository.ListApprovalsByDocument(documentId);

        public IReadOnlyList<DocumentApproval> ListForApprover(int approverId, ApprovalDecision? decision = null)
            => _repository.ListApprovalsByApprover(approverId, decision);

        /// <summary>
        /// True, если <paramref name="actorId"/> может принять решение по
        /// этапу <paramref name="approval"/>: либо это сам согласующий, либо
        /// его активный заместитель (область ApprovalsOnly или Full).
        /// </summary>
        private bool IsAuthorizedActor(DocumentApproval approval, int actorId)
        {
            if (actorId <= 0) return false;
            if (approval.ApproverId == actorId) return true;
            if (_substitution == null) return false;

            var sub = _substitution.GetActiveSubstitute(
                approval.ApproverId, DateTime.Now, SubstitutionScope.ApprovalsOnly);
            return sub?.SubstituteEmployeeId == actorId;
        }
    }
}
