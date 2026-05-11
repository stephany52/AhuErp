using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="IContractRepository"/>. Контракты
    /// хранятся в TPH-таблице <c>Documents</c>; репозиторий фильтрует через
    /// <see cref="DbSet{Contract}"/> и не пересекается с обычными РКК.</summary>
    public sealed class EfContractRepository : IContractRepository
    {
        private readonly AhuDbContext _ctx;

        public EfContractRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public Contract Add(Contract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (string.IsNullOrWhiteSpace(contract.Title))
                throw new ArgumentException("Наименование контракта обязательно.", nameof(contract));
            if (contract.ContractAmount <= 0)
                throw new ArgumentException("Цена контракта должна быть положительной.", nameof(contract));

            _ctx.Contracts.Add(contract);
            _ctx.SaveChanges();
            return contract;
        }

        public Contract Update(Contract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            _ctx.Entry(contract).State = EntityState.Modified;
            _ctx.SaveChanges();
            return contract;
        }

        public Contract Get(int id) => _ctx.Contracts.Find(id);

        public IReadOnlyList<Contract> List()
            => _ctx.Contracts.OrderByDescending(c => c.CreationDate).ToList().AsReadOnly();

        public IReadOnlyList<Contract> ListByProcedure(int procedureId)
            => _ctx.Contracts
                .Where(c => c.ProcurementProcedureId == procedureId)
                .OrderByDescending(c => c.CreationDate)
                .ToList()
                .AsReadOnly();

        public ContractMilestone AddMilestone(ContractMilestone milestone)
        {
            if (milestone == null) throw new ArgumentNullException(nameof(milestone));
            if (_ctx.Contracts.Find(milestone.ContractId) == null)
                throw new InvalidOperationException(
                    $"Контракт #{milestone.ContractId} не найден.");
            if (string.IsNullOrWhiteSpace(milestone.Title))
                throw new ArgumentException("Наименование этапа обязательно.", nameof(milestone));
            if (milestone.Amount < 0)
                throw new ArgumentException("Сумма этапа не может быть отрицательной.", nameof(milestone));

            _ctx.ContractMilestones.Add(milestone);
            _ctx.SaveChanges();
            return milestone;
        }

        public ContractMilestone UpdateMilestone(ContractMilestone milestone)
        {
            if (milestone == null) throw new ArgumentNullException(nameof(milestone));
            _ctx.Entry(milestone).State = EntityState.Modified;
            _ctx.SaveChanges();
            return milestone;
        }

        public ContractMilestone GetMilestone(int milestoneId)
            => _ctx.ContractMilestones.Find(milestoneId);

        public IReadOnlyList<ContractMilestone> ListMilestones(int contractId)
            => _ctx.ContractMilestones
                .Where(m => m.ContractId == contractId)
                .OrderBy(m => m.SequenceNumber)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<ContractMilestone> ListUpcomingMilestones(DateTime from, DateTime to)
            => _ctx.ContractMilestones
                .Where(m => (m.Status == ContractMilestoneStatus.Planned
                             || m.Status == ContractMilestoneStatus.InProgress)
                            && m.PlannedDate >= from
                            && m.PlannedDate <= to)
                .OrderBy(m => m.PlannedDate)
                .ToList()
                .AsReadOnly();
    }
}
