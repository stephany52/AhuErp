using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация <see cref="IContractRepository"/> для тестов.</summary>
    public sealed class InMemoryContractRepository : IContractRepository
    {
        private readonly Dictionary<int, Contract> _contracts = new Dictionary<int, Contract>();
        private readonly Dictionary<int, ContractMilestone> _milestones = new Dictionary<int, ContractMilestone>();
        private int _nextContractId = 1;
        private int _nextMilestoneId = 1;

        public Contract Add(Contract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (string.IsNullOrWhiteSpace(contract.Title))
                throw new ArgumentException("Наименование контракта обязательно.", nameof(contract));
            if (contract.ContractAmount <= 0)
                throw new ArgumentException("Цена контракта должна быть положительной.", nameof(contract));

            contract.Id = _nextContractId++;
            _contracts[contract.Id] = contract;
            return contract;
        }

        public Contract Update(Contract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (!_contracts.ContainsKey(contract.Id))
                throw new InvalidOperationException("Контракт не найден.");
            _contracts[contract.Id] = contract;
            return contract;
        }

        public Contract Get(int id) => _contracts.TryGetValue(id, out var c) ? c : null;

        public IReadOnlyList<Contract> List()
            => _contracts.Values.OrderByDescending(c => c.CreationDate).ToList().AsReadOnly();

        public IReadOnlyList<Contract> ListByProcedure(int procedureId)
            => _contracts.Values.Where(c => c.ProcurementProcedureId == procedureId)
                .OrderByDescending(c => c.CreationDate).ToList().AsReadOnly();

        public ContractMilestone AddMilestone(ContractMilestone milestone)
        {
            if (milestone == null) throw new ArgumentNullException(nameof(milestone));
            if (!_contracts.ContainsKey(milestone.ContractId))
                throw new InvalidOperationException(
                    $"Контракт #{milestone.ContractId} не найден.");
            if (string.IsNullOrWhiteSpace(milestone.Title))
                throw new ArgumentException("Наименование этапа обязательно.", nameof(milestone));
            if (milestone.Amount < 0)
                throw new ArgumentException("Сумма этапа не может быть отрицательной.", nameof(milestone));

            milestone.Id = _nextMilestoneId++;
            _milestones[milestone.Id] = milestone;
            return milestone;
        }

        public ContractMilestone UpdateMilestone(ContractMilestone milestone)
        {
            if (milestone == null) throw new ArgumentNullException(nameof(milestone));
            if (!_milestones.ContainsKey(milestone.Id))
                throw new InvalidOperationException("Этап не найден.");
            _milestones[milestone.Id] = milestone;
            return milestone;
        }

        public ContractMilestone GetMilestone(int milestoneId)
            => _milestones.TryGetValue(milestoneId, out var m) ? m : null;

        public IReadOnlyList<ContractMilestone> ListMilestones(int contractId)
            => _milestones.Values.Where(m => m.ContractId == contractId)
                .OrderBy(m => m.SequenceNumber).ToList().AsReadOnly();

        public IReadOnlyList<ContractMilestone> ListUpcomingMilestones(DateTime from, DateTime to)
            => _milestones.Values
                .Where(m => (m.Status == ContractMilestoneStatus.Planned
                             || m.Status == ContractMilestoneStatus.InProgress)
                            && m.PlannedDate >= from
                            && m.PlannedDate <= to)
                .OrderBy(m => m.PlannedDate)
                .ToList()
                .AsReadOnly();
    }
}
