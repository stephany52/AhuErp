using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Фасад управления планами-графиками закупок, процедурами и контрактами
    /// (Phase 20 / Improvement #13). Инкапсулирует state-machine плана/процедуры/
    /// контракта, аудит, валидацию и идемпотентную рассылку напоминаний
    /// о приближении срока этапа.
    /// </summary>
    public interface IProcurementService
    {
        // --------- План закупок ----------------------------------------

        ProcurementPlan CreatePlan(int year, string title, int actorId, string notes = null);
        ProcurementPlan ApprovePlan(int planId, int approverId);
        ProcurementPlan PublishPlan(int planId, string eisRegistrationNumber, int actorId);
        ProcurementPlan ClosePlan(int planId, int actorId);
        ProcurementPlan GetPlan(int id);
        IReadOnlyList<ProcurementPlan> ListPlans();

        // --------- Позиции плана ---------------------------------------

        ProcurementPlanItem AddPlanItem(int planId, ProcurementPlanItem item, int actorId);
        IReadOnlyList<ProcurementPlanItem> ListPlanItems(int planId);

        // --------- Процедуры -------------------------------------------

        ProcurementProcedure RegisterProcedure(ProcurementProcedure procedure, int actorId);
        ProcurementProcedure AnnounceProcedure(int procedureId, string eisNoticeNumber,
                                               DateTime announcedAt, DateTime bidsDeadline,
                                               int actorId);
        ProcurementProcedure AwardProcedure(int procedureId, string supplierInn, string supplierName,
                                            decimal awardedPrice, DateTime decisionAt, int actorId);
        ProcurementProcedure CancelProcedure(int procedureId, int actorId);
        ProcurementProcedure GetProcedure(int id);

        // --------- Контракты -------------------------------------------

        Contract RegisterContract(Contract contract, int actorId);
        Contract SignContract(int contractId, DateTime signedAt, int actorId);
        Contract MarkContractExecuted(int contractId, DateTime executedAt, int actorId);
        Contract TerminateContract(int contractId, int actorId, string reason);
        Contract GetContract(int id);
        IReadOnlyList<Contract> ListContracts();

        // --------- Этапы исполнения ------------------------------------

        ContractMilestone AddMilestone(int contractId, ContractMilestone milestone, int actorId);
        ContractMilestone CompleteMilestone(int milestoneId, DateTime actualDate, int actorId);

        /// <summary>
        /// Идемпотентный обход активных этапов исполнения контрактов: создаёт
        /// <see cref="NotificationKind.ContractMilestoneDueSoon"/> за N дней до
        /// плановой даты (по умолчанию <see cref="ProcurementService.DefaultMilestoneReminderDays"/>)
        /// и <see cref="NotificationKind.ContractMilestoneOverdue"/> при просрочке.
        /// Повторный вызов в рамках того же события записей не дублирует.
        /// </summary>
        void TickMilestoneReminders(DateTime now, int reminderDays = 7);
    }
}
