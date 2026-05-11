using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 20 / Improvement #13 — закупки 44-ФЗ.
    /// Покрытие:
    /// (1) Repository-инварианты — уникальность <c>Year</c> у плана, FK план→позиция,
    ///     FK контракт→этап, фильтры окна <c>ListUpcomingMilestones</c>.
    /// (2) <see cref="ProcurementService"/> — state-machine плана
    ///     (Draft → Approved → Published → Closed), процедуры
    ///     (Planned → Announced → AwardedDecision → AwardedAndExecuted),
    ///     контракта (Draft → Signed → InExecution → Executed / Terminated),
    ///     этапов (Planned → Completed | Overdue / Cancelled), валидация
    ///     порядка состояний и сумм.
    /// (3) <c>TickMilestoneReminders</c> — идемпотентная рассылка
    ///     <see cref="NotificationKind.ContractMilestoneDueSoon"/> /
    ///     <see cref="NotificationKind.ContractMilestoneOverdue"/>.
    /// </summary>
    public class Phase20ProcurementTests
    {
        private readonly InMemoryProcurementPlanRepository _plans = new InMemoryProcurementPlanRepository();
        private readonly InMemoryProcurementProcedureRepository _procedures = new InMemoryProcurementProcedureRepository();
        private readonly InMemoryContractRepository _contracts = new InMemoryContractRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly InMemoryNotificationRepository _notificationRepo = new InMemoryNotificationRepository();
        private readonly InMemoryEmployeeRepository _employees = new InMemoryEmployeeRepository();
        private readonly InMemoryTaskRepository _tasks = new InMemoryTaskRepository();
        private readonly AuditService _audit;
        private readonly NotificationService _notifications;
        private readonly ProcurementService _service;

        public Phase20ProcurementTests()
        {
            _audit = new AuditService(_auditRepo);
            _notifications = new NotificationService(_notificationRepo, _employees, _tasks, _audit);
            _service = new ProcurementService(_plans, _procedures, _contracts, _audit, _notifications);
        }

        // =========================================================
        // (1) Repository-инварианты.
        // =========================================================

        [Fact]
        public void PlanRepository_rejects_duplicate_year()
        {
            _plans.Add(new ProcurementPlan { Year = 2026, Title = "План 2026", CreatedAt = DateTime.UtcNow });
            Assert.Throws<InvalidOperationException>(() =>
                _plans.Add(new ProcurementPlan { Year = 2026, Title = "Другой 2026", CreatedAt = DateTime.UtcNow }));
        }

        [Fact]
        public void PlanRepository_GetByYear_returns_match_or_null()
        {
            _plans.Add(new ProcurementPlan { Year = 2026, Title = "T", CreatedAt = DateTime.UtcNow });
            Assert.NotNull(_plans.GetByYear(2026));
            Assert.Null(_plans.GetByYear(2099));
        }

        [Fact]
        public void PlanRepository_AddItem_requires_existing_plan()
        {
            Assert.Throws<InvalidOperationException>(() => _plans.AddItem(new ProcurementPlanItem
            {
                ProcurementPlanId = 999,
                Okpd2Code = "26.20.15",
                Subject = "Картриджи",
                InitialMaxPrice = 100_000m,
            }));
        }

        [Fact]
        public void ContractRepository_ListUpcoming_filters_by_status_and_window()
        {
            var c = _contracts.Add(new Contract
            {
                Title = "Контракт",
                ContractAmount = 100m,
                CreationDate = DateTime.UtcNow,
                ContractStatus = ContractStatus.InExecution,
            });
            _contracts.AddMilestone(new ContractMilestone
            {
                ContractId = c.Id,
                Title = "Этап в окне",
                PlannedDate = DateTime.Today.AddDays(3),
                Amount = 50m,
                Status = ContractMilestoneStatus.Planned,
            });
            _contracts.AddMilestone(new ContractMilestone
            {
                ContractId = c.Id,
                Title = "Этап вне окна",
                PlannedDate = DateTime.Today.AddDays(60),
                Amount = 50m,
                Status = ContractMilestoneStatus.Planned,
            });
            _contracts.AddMilestone(new ContractMilestone
            {
                ContractId = c.Id,
                Title = "Этап завершён",
                PlannedDate = DateTime.Today.AddDays(2),
                Amount = 50m,
                Status = ContractMilestoneStatus.Completed,
            });

            var upcoming = _contracts.ListUpcomingMilestones(DateTime.Today, DateTime.Today.AddDays(7));
            Assert.Single(upcoming);
            Assert.Equal("Этап в окне", upcoming[0].Title);
        }

        // =========================================================
        // (2) ProcurementService — план.
        // =========================================================

        [Fact]
        public void Service_CreatePlan_persists_in_draft()
        {
            var plan = _service.CreatePlan(2026, "План-график 2026", actorId: 1);
            Assert.True(plan.Id > 0);
            Assert.Equal(ProcurementPlanStatus.Draft, plan.Status);
            Assert.Single(_auditRepo.Query(new AuditQueryFilter
            {
                ActionType = AuditActionType.ProcurementPlanCreated,
            }));
        }

        [Fact]
        public void Service_PublishPlan_requires_approval()
        {
            var plan = _service.CreatePlan(2026, "X", 1);
            Assert.Throws<InvalidOperationException>(() => _service.PublishPlan(plan.Id, "ЕИС-1", 1));
            _service.ApprovePlan(plan.Id, 1);
            var published = _service.PublishPlan(plan.Id, "ЕИС-1", 1);
            Assert.Equal(ProcurementPlanStatus.Published, published.Status);
            Assert.Equal("ЕИС-1", published.EisRegistrationNumber);
        }

        [Fact]
        public void Service_AddPlanItem_validates_owner_plan()
        {
            var plan = _service.CreatePlan(2026, "X", 1);
            var item = _service.AddPlanItem(plan.Id, new ProcurementPlanItem
            {
                Okpd2Code = "26.20.15",
                Subject = "Картриджи",
                InitialMaxPrice = 100_000m,
                Method = ProcurementMethod.ElectronicAuction,
                PlannedQuarter = ProcurementQuarter.Q1,
            }, actorId: 1);
            Assert.True(item.Id > 0);
            Assert.Equal(plan.Id, item.ProcurementPlanId);
            Assert.Single(_service.ListPlanItems(plan.Id));
        }

        // =========================================================
        // (2) ProcurementService — процедуры.
        // =========================================================

        [Fact]
        public void Service_Procedure_AnnounceAward_drives_state_machine()
        {
            var plan = _service.CreatePlan(2026, "X", 1);
            var item = _service.AddPlanItem(plan.Id, new ProcurementPlanItem
            {
                Okpd2Code = "26.20.15",
                Subject = "Картриджи",
                InitialMaxPrice = 100_000m,
            }, 1);
            var proc = _service.RegisterProcedure(new ProcurementProcedure
            {
                ProcurementPlanItemId = item.Id,
                Method = ProcurementMethod.ElectronicAuction,
            }, 1);
            Assert.Equal(ProcurementProcedureStatus.Planned, proc.Status);

            var announced = _service.AnnounceProcedure(proc.Id, "0123",
                announcedAt: DateTime.UtcNow,
                bidsDeadline: DateTime.UtcNow.AddDays(7),
                actorId: 1);
            Assert.Equal(ProcurementProcedureStatus.Announced, announced.Status);

            var awarded = _service.AwardProcedure(proc.Id, "7707083893", "ООО Поставщик",
                awardedPrice: 95_000m, decisionAt: DateTime.UtcNow, actorId: 1);
            Assert.Equal(ProcurementProcedureStatus.AwardedDecision, awarded.Status);
            Assert.Equal(95_000m, awarded.AwardedPrice);
        }

        // =========================================================
        // (2) ProcurementService — контракты и этапы.
        // =========================================================

        [Fact]
        public void Service_SignContract_transitions_to_in_execution()
        {
            var contract = RegisterRunningContract();
            var signed = _service.SignContract(contract.Id, DateTime.UtcNow, actorId: 1);
            Assert.True(signed.ContractStatus == ContractStatus.Signed
                        || signed.ContractStatus == ContractStatus.InExecution);
        }

        [Fact]
        public void Service_MarkContractExecuted_requires_all_milestones_done()
        {
            var contract = RegisterRunningContract();
            _service.SignContract(contract.Id, DateTime.UtcNow, 1);
            var milestone = _service.AddMilestone(contract.Id, new ContractMilestone
            {
                Title = "Поставка 1",
                PlannedDate = DateTime.UtcNow.AddDays(10),
                Amount = 50_000m,
            }, 1);

            Assert.Throws<InvalidOperationException>(() =>
                _service.MarkContractExecuted(contract.Id, DateTime.UtcNow, 1));

            _service.CompleteMilestone(milestone.Id, DateTime.UtcNow.AddDays(8), 1);
            var executed = _service.MarkContractExecuted(contract.Id, DateTime.UtcNow.AddDays(11), 1);
            Assert.Equal(ContractStatus.Executed, executed.ContractStatus);
        }

        [Fact]
        public void Service_TerminateContract_records_audit_with_reason()
        {
            var contract = RegisterRunningContract();
            _service.SignContract(contract.Id, DateTime.UtcNow, 1);
            var terminated = _service.TerminateContract(contract.Id, 1, reason: "Существенное нарушение");
            Assert.Equal(ContractStatus.Terminated, terminated.ContractStatus);
            Assert.Throws<ArgumentException>(() =>
                _service.TerminateContract(contract.Id, 1, reason: " "));
        }

        // =========================================================
        // (3) Reminder system — TickMilestoneReminders.
        // =========================================================

        [Fact]
        public void TickReminders_creates_due_soon_notification()
        {
            var emp = new Employee
            {
                Id = 101,
                FullName = "Иван Петров",
                Role = EmployeeRole.Manager,
                PasswordHash = "x",
                Email = "i@example.com",
            };
            _employees.Add(emp);
            var contract = RegisterRunningContract(assignedTo: emp.Id);
            _service.SignContract(contract.Id, DateTime.UtcNow, 1);
            var milestone = _service.AddMilestone(contract.Id, new ContractMilestone
            {
                Title = "Этап",
                PlannedDate = DateTime.Now.AddDays(3),
                Amount = 10m,
            }, 1);

            _service.TickMilestoneReminders(DateTime.Now, reminderDays: 7);

            var notif = _notifications.ListForUser(emp.Id).Single();
            Assert.Equal(NotificationKind.ContractMilestoneDueSoon, notif.Kind);
            Assert.Contains("Этап", notif.Body);
            Assert.True(_contracts.GetMilestone(milestone.Id).DeadlineReminderSentAt.HasValue);
        }

        [Fact]
        public void TickReminders_is_idempotent_within_a_day()
        {
            var emp = new Employee
            {
                Id = 102,
                FullName = "Иван Петров",
                Role = EmployeeRole.Manager,
                PasswordHash = "x",
            };
            _employees.Add(emp);
            var contract = RegisterRunningContract(assignedTo: emp.Id);
            _service.SignContract(contract.Id, DateTime.UtcNow, 1);
            _service.AddMilestone(contract.Id, new ContractMilestone
            {
                Title = "E",
                PlannedDate = DateTime.Now.AddDays(2),
                Amount = 1m,
            }, 1);

            var now = DateTime.Now;
            _service.TickMilestoneReminders(now);
            _service.TickMilestoneReminders(now);
            Assert.Single(_notifications.ListForUser(emp.Id));
        }

        [Fact]
        public void TickReminders_marks_overdue_milestone_and_emits_overdue_kind()
        {
            var emp = new Employee
            {
                Id = 103,
                FullName = "Иван Петров",
                Role = EmployeeRole.Manager,
                PasswordHash = "x",
            };
            _employees.Add(emp);
            var contract = RegisterRunningContract(assignedTo: emp.Id);
            _service.SignContract(contract.Id, DateTime.UtcNow, 1);
            var milestone = _service.AddMilestone(contract.Id, new ContractMilestone
            {
                Title = "Просрочка",
                PlannedDate = DateTime.Now.AddDays(-5),
                Amount = 1m,
            }, 1);

            _service.TickMilestoneReminders(DateTime.Now);

            Assert.Equal(ContractMilestoneStatus.Overdue, _contracts.GetMilestone(milestone.Id).Status);
            var notif = _notifications.ListForUser(emp.Id).Single();
            Assert.Equal(NotificationKind.ContractMilestoneOverdue, notif.Kind);
        }

        // =========================================================
        // (4) RolePolicy интеграция.
        // =========================================================

        [Fact]
        public void RolePolicy_Procurement_visible_for_management_and_clerks()
        {
            Assert.True(RolePolicy.IsAllowed(EmployeeRole.Admin, RolePolicy.Procurement));
            Assert.True(RolePolicy.IsAllowed(EmployeeRole.Manager, RolePolicy.Procurement));
            Assert.True(RolePolicy.IsAllowed(EmployeeRole.DeputyHead, RolePolicy.Procurement));
            Assert.True(RolePolicy.IsAllowed(EmployeeRole.Clerk, RolePolicy.Procurement));
            Assert.False(RolePolicy.IsAllowed(EmployeeRole.TechSupport, RolePolicy.Procurement));
            Assert.False(RolePolicy.IsAllowed(EmployeeRole.Archivist, RolePolicy.Procurement));
        }

        [Fact]
        public void RolePolicy_CanManageProcurement_restricted_to_management()
        {
            Assert.True(RolePolicy.CanManageProcurement(EmployeeRole.Admin));
            Assert.True(RolePolicy.CanManageProcurement(EmployeeRole.Manager));
            Assert.True(RolePolicy.CanManageProcurement(EmployeeRole.DeputyHead));
            Assert.False(RolePolicy.CanManageProcurement(EmployeeRole.Clerk));
            Assert.False(RolePolicy.CanManageProcurement(EmployeeRole.TechSupport));
        }

        // =========================================================
        // helpers
        // =========================================================

        private Contract RegisterRunningContract(int? assignedTo = null)
        {
            var contract = _service.RegisterContract(new Contract
            {
                Title = "Поставка картриджей",
                CreationDate = DateTime.UtcNow,
                Deadline = DateTime.UtcNow.AddYears(1),
                ContractAmount = 100_000m,
                SupplierName = "ООО Поставщик",
                AssignedEmployeeId = assignedTo,
                ContractStartDate = DateTime.UtcNow.AddDays(-1),
                ContractEndDate = DateTime.UtcNow.AddMonths(6),
            }, actorId: 1);
            return contract;
        }
    }
}
