using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 20 / Improvement #13 — управление планами-графиками закупок,
    /// процедурами и контрактами 44-ФЗ. Инкапсулирует state-machine плана
    /// (Draft → Approved → Published → Closed), процедуры (Planned →
    /// Announced → BidsAccepted → BidsEvaluation → AwardedDecision →
    /// AwardedAndExecuted | Cancelled | Failed) и контракта (Draft →
    /// Signed → InExecution → Executed | Terminated | Cancelled).
    /// Использует <see cref="IAuditService"/> для журнала действий и
    /// <see cref="INotificationService"/> для напоминаний о приближении
    /// срока этапа.
    /// </summary>
    public sealed class ProcurementService : IProcurementService
    {
        /// <summary>
        /// Окно напоминаний по умолчанию — 7 дней до плановой даты этапа.
        /// </summary>
        public const int DefaultMilestoneReminderDays = 7;

        private readonly IProcurementPlanRepository _planRepo;
        private readonly IProcurementProcedureRepository _procedureRepo;
        private readonly IContractRepository _contractRepo;
        private readonly IAuditService _audit;
        private readonly INotificationService _notifications;

        public ProcurementService(
            IProcurementPlanRepository planRepo,
            IProcurementProcedureRepository procedureRepo,
            IContractRepository contractRepo,
            IAuditService audit,
            INotificationService notifications = null)
        {
            _planRepo = planRepo ?? throw new ArgumentNullException(nameof(planRepo));
            _procedureRepo = procedureRepo ?? throw new ArgumentNullException(nameof(procedureRepo));
            _contractRepo = contractRepo ?? throw new ArgumentNullException(nameof(contractRepo));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _notifications = notifications;
        }

        // =================================================================
        // План закупок.
        // =================================================================

        public ProcurementPlan CreatePlan(int year, string title, int actorId, string notes = null)
        {
            if (year <= 0) throw new ArgumentException("Год обязателен.", nameof(year));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Наименование плана обязательно.", nameof(title));

            var plan = new ProcurementPlan
            {
                Year = year,
                Title = title,
                Status = ProcurementPlanStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                Notes = notes,
            };
            var saved = _planRepo.Add(plan);
            _audit.Record(AuditActionType.ProcurementPlanCreated, "ProcurementPlan", saved.Id, actorId,
                details: $"Создан план закупок на {year} год: «{title}».");
            return saved;
        }

        public ProcurementPlan ApprovePlan(int planId, int approverId)
        {
            var plan = _planRepo.Get(planId)
                ?? throw new InvalidOperationException($"План закупок #{planId} не найден.");
            if (plan.Status != ProcurementPlanStatus.Draft)
                throw new InvalidOperationException(
                    $"Утвердить можно только план в статусе Draft (текущий: {plan.Status}).");

            plan.Status = ProcurementPlanStatus.Approved;
            plan.ApprovedAt = DateTime.UtcNow;
            plan.ApprovedByEmployeeId = approverId;
            _planRepo.Update(plan);
            _audit.Record(AuditActionType.ProcurementPlanApproved, "ProcurementPlan", plan.Id, approverId,
                details: $"Утверждён план закупок на {plan.Year} год.");
            return plan;
        }

        public ProcurementPlan PublishPlan(int planId, string eisRegistrationNumber, int actorId)
        {
            if (string.IsNullOrWhiteSpace(eisRegistrationNumber))
                throw new ArgumentException(
                    "Регистрационный номер в ЕИС обязателен для публикации.", nameof(eisRegistrationNumber));

            var plan = _planRepo.Get(planId)
                ?? throw new InvalidOperationException($"План закупок #{planId} не найден.");
            if (plan.Status != ProcurementPlanStatus.Approved)
                throw new InvalidOperationException(
                    $"Опубликовать можно только утверждённый план (текущий: {plan.Status}).");

            plan.Status = ProcurementPlanStatus.Published;
            plan.PublishedAt = DateTime.UtcNow;
            plan.EisRegistrationNumber = eisRegistrationNumber;
            _planRepo.Update(plan);
            _audit.Record(AuditActionType.ProcurementPlanPublished, "ProcurementPlan", plan.Id, actorId,
                details: $"План закупок на {plan.Year} год опубликован в ЕИС (№ {eisRegistrationNumber}).");
            return plan;
        }

        public ProcurementPlan ClosePlan(int planId, int actorId)
        {
            var plan = _planRepo.Get(planId)
                ?? throw new InvalidOperationException($"План закупок #{planId} не найден.");
            if (plan.Status == ProcurementPlanStatus.Closed)
                throw new InvalidOperationException("План уже закрыт.");

            plan.Status = ProcurementPlanStatus.Closed;
            _planRepo.Update(plan);
            _audit.Record(AuditActionType.ProcurementPlanClosed, "ProcurementPlan", plan.Id, actorId,
                details: $"План закупок на {plan.Year} год закрыт.");
            return plan;
        }

        public ProcurementPlan GetPlan(int id) => _planRepo.Get(id);

        public IReadOnlyList<ProcurementPlan> ListPlans() => _planRepo.List();

        // =================================================================
        // Позиции плана.
        // =================================================================

        public ProcurementPlanItem AddPlanItem(int planId, ProcurementPlanItem item, int actorId)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            var plan = _planRepo.Get(planId)
                ?? throw new InvalidOperationException($"План закупок #{planId} не найден.");
            if (plan.Status == ProcurementPlanStatus.Closed)
                throw new InvalidOperationException("Нельзя добавлять позиции в закрытый план.");

            item.ProcurementPlanId = planId;
            if (item.LineNumber <= 0)
            {
                var existing = _planRepo.ListItems(planId);
                item.LineNumber = existing.Count + 1;
            }
            var saved = _planRepo.AddItem(item);
            _audit.Record(AuditActionType.ProcurementPlanItemAdded, "ProcurementPlanItem", saved.Id, actorId,
                details: $"В план #{planId} добавлена позиция «{saved.Subject}» (ОКПД2 {saved.Okpd2Code}).");
            return saved;
        }

        public IReadOnlyList<ProcurementPlanItem> ListPlanItems(int planId)
            => _planRepo.ListItems(planId);

        // =================================================================
        // Процедуры определения поставщика.
        // =================================================================

        public ProcurementProcedure RegisterProcedure(ProcurementProcedure procedure, int actorId)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            var item = _planRepo.GetItem(procedure.ProcurementPlanItemId)
                ?? throw new InvalidOperationException(
                    $"Позиция плана #{procedure.ProcurementPlanItemId} не найдена.");

            procedure.Status = ProcurementProcedureStatus.Planned;
            if (procedure.Method == default)
            {
                procedure.Method = item.Method;
            }
            return _procedureRepo.Add(procedure);
        }

        public ProcurementProcedure AnnounceProcedure(int procedureId, string eisNoticeNumber,
            DateTime announcedAt, DateTime bidsDeadline, int actorId)
        {
            if (string.IsNullOrWhiteSpace(eisNoticeNumber))
                throw new ArgumentException("Номер извещения в ЕИС обязателен.", nameof(eisNoticeNumber));
            if (bidsDeadline <= announcedAt)
                throw new ArgumentException("Срок подачи заявок должен быть позже даты размещения.", nameof(bidsDeadline));

            var procedure = _procedureRepo.Get(procedureId)
                ?? throw new InvalidOperationException($"Процедура #{procedureId} не найдена.");
            if (procedure.Status != ProcurementProcedureStatus.Planned)
                throw new InvalidOperationException(
                    $"Объявить можно только запланированную процедуру (текущий статус: {procedure.Status}).");

            procedure.EisNoticeNumber = eisNoticeNumber;
            procedure.AnnouncedAt = announcedAt;
            procedure.BidsDeadline = bidsDeadline;
            procedure.Status = ProcurementProcedureStatus.Announced;
            _procedureRepo.Update(procedure);

            _audit.Record(AuditActionType.ProcurementProcedureAnnounced, "ProcurementProcedure", procedure.Id, actorId,
                details: $"Размещено извещение № {eisNoticeNumber}, срок подачи заявок {bidsDeadline:dd.MM.yyyy HH:mm}.");
            return procedure;
        }

        public ProcurementProcedure AwardProcedure(int procedureId, string supplierInn, string supplierName,
            decimal awardedPrice, DateTime decisionAt, int actorId)
        {
            if (string.IsNullOrWhiteSpace(supplierInn))
                throw new ArgumentException("ИНН победителя обязателен.", nameof(supplierInn));
            if (string.IsNullOrWhiteSpace(supplierName))
                throw new ArgumentException("Наименование победителя обязательно.", nameof(supplierName));
            if (awardedPrice <= 0)
                throw new ArgumentException("Цена контракта должна быть положительной.", nameof(awardedPrice));

            var procedure = _procedureRepo.Get(procedureId)
                ?? throw new InvalidOperationException($"Процедура #{procedureId} не найдена.");
            if (procedure.Status == ProcurementProcedureStatus.AwardedDecision
                || procedure.Status == ProcurementProcedureStatus.AwardedAndExecuted
                || procedure.Status == ProcurementProcedureStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    $"Подвести итоги нельзя для процедуры в статусе {procedure.Status}.");
            }

            procedure.AwardedSupplierInn = supplierInn;
            procedure.AwardedSupplierName = supplierName;
            procedure.AwardedPrice = awardedPrice;
            procedure.AwardDecisionAt = decisionAt;
            procedure.Status = ProcurementProcedureStatus.AwardedDecision;
            _procedureRepo.Update(procedure);

            _audit.Record(AuditActionType.ProcurementProcedureAwarded, "ProcurementProcedure", procedure.Id, actorId,
                details: $"Подведены итоги: победитель {supplierName} (ИНН {supplierInn}), цена {awardedPrice:N2} ₽.");
            return procedure;
        }

        public ProcurementProcedure CancelProcedure(int procedureId, int actorId)
        {
            var procedure = _procedureRepo.Get(procedureId)
                ?? throw new InvalidOperationException($"Процедура #{procedureId} не найдена.");
            if (procedure.Status == ProcurementProcedureStatus.AwardedAndExecuted)
                throw new InvalidOperationException(
                    "Нельзя отменить процедуру с уже исполненным контрактом.");

            procedure.Status = ProcurementProcedureStatus.Cancelled;
            _procedureRepo.Update(procedure);
            _audit.Record(AuditActionType.ProcurementProcedureCancelled, "ProcurementProcedure", procedure.Id, actorId,
                details: $"Процедура #{procedureId} отменена.");
            return procedure;
        }

        public ProcurementProcedure GetProcedure(int id) => _procedureRepo.Get(id);

        // =================================================================
        // Контракты.
        // =================================================================

        public Contract RegisterContract(Contract contract, int actorId)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (string.IsNullOrWhiteSpace(contract.Title))
                throw new ArgumentException("Наименование контракта обязательно.", nameof(contract));
            if (contract.ContractAmount <= 0)
                throw new ArgumentException("Цена контракта должна быть положительной.", nameof(contract));
            if (contract.ContractEndDate.HasValue && contract.ContractStartDate.HasValue
                && contract.ContractEndDate.Value < contract.ContractStartDate.Value)
                throw new ArgumentException("Дата окончания контракта раньше даты начала.", nameof(contract));

            if (contract.ProcurementProcedureId.HasValue)
            {
                var procedure = _procedureRepo.Get(contract.ProcurementProcedureId.Value)
                    ?? throw new InvalidOperationException(
                        $"Процедура #{contract.ProcurementProcedureId} не найдена.");
                if (procedure.Status != ProcurementProcedureStatus.AwardedDecision
                    && procedure.Status != ProcurementProcedureStatus.AwardedAndExecuted)
                {
                    throw new InvalidOperationException(
                        "Контракт можно зарегистрировать только по процедуре с подведёнными итогами.");
                }
            }

            contract.ContractStatus = ContractStatus.Draft;
            if (contract.CreationDate == default)
            {
                contract.CreationDate = DateTime.UtcNow;
            }
            if (contract.Deadline == default && contract.ContractEndDate.HasValue)
            {
                contract.Deadline = contract.ContractEndDate.Value;
            }

            var saved = _contractRepo.Add(contract);
            _audit.Record(AuditActionType.ContractRegistered, "Contract", saved.Id, actorId,
                details: $"Зарегистрирован контракт «{saved.Title}» на сумму {saved.ContractAmount:N2} ₽ с {saved.SupplierName ?? "поставщиком"}.");
            return saved;
        }

        public Contract SignContract(int contractId, DateTime signedAt, int actorId)
        {
            var contract = _contractRepo.Get(contractId)
                ?? throw new InvalidOperationException($"Контракт #{contractId} не найден.");
            if (contract.ContractStatus != ContractStatus.Draft)
                throw new InvalidOperationException(
                    $"Подписать можно только контракт в статусе Draft (текущий: {contract.ContractStatus}).");

            contract.ContractStatus = ContractStatus.Signed;
            contract.SignedAt = signedAt;
            _contractRepo.Update(contract);

            // Сразу переводим в исполнение, если дата начала наступила.
            if (contract.ContractStartDate.HasValue && contract.ContractStartDate.Value <= signedAt)
            {
                contract.ContractStatus = ContractStatus.InExecution;
                _contractRepo.Update(contract);
            }

            _audit.Record(AuditActionType.ContractSigned, "Contract", contract.Id, actorId,
                details: $"Контракт «{contract.Title}» подписан {signedAt:dd.MM.yyyy}.");

            // Если контракт привязан к процедуре закупки и она в стадии
            // AwardedDecision — переводим её в AwardedAndExecuted.
            if (contract.ProcurementProcedureId.HasValue)
            {
                var procedure = _procedureRepo.Get(contract.ProcurementProcedureId.Value);
                if (procedure != null
                    && procedure.Status == ProcurementProcedureStatus.AwardedDecision)
                {
                    procedure.Status = ProcurementProcedureStatus.AwardedAndExecuted;
                    _procedureRepo.Update(procedure);
                }
            }

            return contract;
        }

        public Contract MarkContractExecuted(int contractId, DateTime executedAt, int actorId)
        {
            var contract = _contractRepo.Get(contractId)
                ?? throw new InvalidOperationException($"Контракт #{contractId} не найден.");
            if (contract.ContractStatus != ContractStatus.InExecution
                && contract.ContractStatus != ContractStatus.Signed)
            {
                throw new InvalidOperationException(
                    $"Закрыть исполнением можно только подписанный/исполняемый контракт (текущий: {contract.ContractStatus}).");
            }

            // Все этапы должны быть завершены или отменены — иначе контракт
            // нельзя считать исполненным.
            var milestones = _contractRepo.ListMilestones(contract.Id);
            if (milestones.Any(m => m.Status != ContractMilestoneStatus.Completed
                                    && m.Status != ContractMilestoneStatus.Cancelled))
            {
                throw new InvalidOperationException(
                    "Контракт нельзя закрыть исполнением: остались незавершённые этапы.");
            }

            contract.ContractStatus = ContractStatus.Executed;
            contract.ExecutedAt = executedAt;
            _contractRepo.Update(contract);

            _audit.Record(AuditActionType.ContractExecuted, "Contract", contract.Id, actorId,
                details: $"Контракт «{contract.Title}» закрыт исполнением {executedAt:dd.MM.yyyy}.");
            return contract;
        }

        public Contract TerminateContract(int contractId, int actorId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина расторжения обязательна.", nameof(reason));

            var contract = _contractRepo.Get(contractId)
                ?? throw new InvalidOperationException($"Контракт #{contractId} не найден.");
            if (contract.ContractStatus == ContractStatus.Executed
                || contract.ContractStatus == ContractStatus.Terminated
                || contract.ContractStatus == ContractStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    $"Расторгнуть нельзя контракт в статусе {contract.ContractStatus}.");
            }

            contract.ContractStatus = ContractStatus.Terminated;
            _contractRepo.Update(contract);

            _audit.Record(AuditActionType.ContractTerminated, "Contract", contract.Id, actorId,
                details: $"Контракт «{contract.Title}» расторгнут: {reason}.");
            return contract;
        }

        public Contract GetContract(int id) => _contractRepo.Get(id);

        public IReadOnlyList<Contract> ListContracts() => _contractRepo.List();

        // =================================================================
        // Этапы исполнения.
        // =================================================================

        public ContractMilestone AddMilestone(int contractId, ContractMilestone milestone, int actorId)
        {
            if (milestone == null) throw new ArgumentNullException(nameof(milestone));
            var contract = _contractRepo.Get(contractId)
                ?? throw new InvalidOperationException($"Контракт #{contractId} не найден.");
            if (contract.ContractStatus == ContractStatus.Executed
                || contract.ContractStatus == ContractStatus.Terminated
                || contract.ContractStatus == ContractStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    $"Нельзя добавить этап в контракт в статусе {contract.ContractStatus}.");
            }

            milestone.ContractId = contractId;
            if (milestone.SequenceNumber <= 0)
            {
                var existing = _contractRepo.ListMilestones(contractId);
                milestone.SequenceNumber = existing.Count + 1;
            }
            milestone.Status = ContractMilestoneStatus.Planned;

            var saved = _contractRepo.AddMilestone(milestone);
            _audit.Record(AuditActionType.ContractMilestoneAdded, "ContractMilestone", saved.Id, actorId,
                details: $"К контракту #{contractId} добавлен этап «{saved.Title}» на сумму {saved.Amount:N2} ₽.");
            return saved;
        }

        public ContractMilestone CompleteMilestone(int milestoneId, DateTime actualDate, int actorId)
        {
            var milestone = _contractRepo.GetMilestone(milestoneId)
                ?? throw new InvalidOperationException($"Этап #{milestoneId} не найден.");
            if (milestone.Status == ContractMilestoneStatus.Completed)
                throw new InvalidOperationException("Этап уже завершён.");
            if (milestone.Status == ContractMilestoneStatus.Cancelled)
                throw new InvalidOperationException("Нельзя завершить отменённый этап.");

            milestone.Status = ContractMilestoneStatus.Completed;
            milestone.ActualDate = actualDate;
            _contractRepo.UpdateMilestone(milestone);

            _audit.Record(AuditActionType.ContractMilestoneCompleted, "ContractMilestone", milestone.Id, actorId,
                details: $"Завершён этап «{milestone.Title}» контракта #{milestone.ContractId} ({actualDate:dd.MM.yyyy}).");
            return milestone;
        }

        public void TickMilestoneReminders(DateTime now, int reminderDays = DefaultMilestoneReminderDays)
        {
            if (reminderDays < 0)
                throw new ArgumentException("Окно напоминания должно быть неотрицательным.", nameof(reminderDays));

            // Шаг 1. Помечаем просроченные и отправляем уведомление.
            // Берём активные этапы со сроком от далёкого прошлого до окна напоминания.
            var windowEnd = now.AddDays(reminderDays);
            var horizonStart = now.AddYears(-10);
            var upcoming = _contractRepo.ListUpcomingMilestones(horizonStart, windowEnd);

            foreach (var milestone in upcoming)
            {
                var isOverdue = milestone.PlannedDate < now;
                var kind = isOverdue
                    ? NotificationKind.ContractMilestoneOverdue
                    : NotificationKind.ContractMilestoneDueSoon;

                if (milestone.DeadlineReminderSentAt.HasValue
                    && milestone.DeadlineReminderSentAt.Value >= now.Date)
                {
                    // Уже отправляли сегодня — пропускаем (идемпотентность).
                    continue;
                }

                if (isOverdue)
                {
                    milestone.Status = ContractMilestoneStatus.Overdue;
                }

                milestone.DeadlineReminderSentAt = now;
                _contractRepo.UpdateMilestone(milestone);

                if (_notifications == null) continue;
                var contract = _contractRepo.Get(milestone.ContractId);
                var recipient = contract?.AssignedEmployeeId ?? contract?.AuthorId ?? 0;
                if (recipient <= 0) continue;

                var title = isOverdue
                    ? $"Просрочен этап контракта №{contract?.RegistrationNumber ?? milestone.ContractId.ToString()}"
                    : $"Близок срок этапа контракта №{contract?.RegistrationNumber ?? milestone.ContractId.ToString()}";
                var body = $"Этап «{milestone.Title}» (плановая дата {milestone.PlannedDate:dd.MM.yyyy}, сумма {milestone.Amount:N2} ₽).";
                _notifications.Create(recipient, kind, title, body, docId: contract?.Id, createdAt: now);
            }
        }
    }
}
