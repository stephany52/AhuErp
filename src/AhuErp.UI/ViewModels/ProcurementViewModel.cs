using System;
using System.Collections.ObjectModel;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Phase 20 / Improvement #13 — карточка раздела «Закупки 44-ФЗ».
    /// Реализована как stub в админ-панели (по требованию приёмки):
    /// показывает планы-графики, процедуры, контракты и ближайшие этапы
    /// исполнения; редактирование стартует на следующей итерации.
    /// </summary>
    public partial class ProcurementViewModel : ViewModelBase
    {
        private readonly IProcurementService _procurement;

        public ObservableCollection<ProcurementPlan> Plans { get; } = new ObservableCollection<ProcurementPlan>();
        public ObservableCollection<Contract> Contracts { get; } = new ObservableCollection<Contract>();
        public ObservableCollection<ContractMilestone> UpcomingMilestones { get; }
            = new ObservableCollection<ContractMilestone>();

        [ObservableProperty]
        private int planCount;

        [ObservableProperty]
        private int activeProcedureCount;

        [ObservableProperty]
        private int contractsInExecutionCount;

        [ObservableProperty]
        private decimal totalCommittedAmount;

        [ObservableProperty]
        private string statusMessage;

        public ProcurementViewModel(IProcurementService procurement)
        {
            _procurement = procurement ?? throw new ArgumentNullException(nameof(procurement));
            Refresh();
        }

        [RelayCommand]
        public void Refresh()
        {
            try
            {
                Plans.Clear();
                foreach (var p in _procurement.ListPlans())
                {
                    Plans.Add(p);
                }
                PlanCount = Plans.Count;

                Contracts.Clear();
                var allContracts = _procurement.ListContracts();
                foreach (var c in allContracts)
                {
                    Contracts.Add(c);
                }
                ContractsInExecutionCount = allContracts.Count(c =>
                    c.ContractStatus == ContractStatus.InExecution
                    || c.ContractStatus == ContractStatus.Signed);
                TotalCommittedAmount = allContracts
                    .Where(c => c.ContractStatus != ContractStatus.Cancelled
                                && c.ContractStatus != ContractStatus.Terminated)
                    .Sum(c => c.ContractAmount);

                UpcomingMilestones.Clear();
                foreach (var c in allContracts)
                {
                    var milestones = _procurement.GetContract(c.Id)?.Milestones ?? new System.Collections.Generic.HashSet<ContractMilestone>();
                    foreach (var m in milestones)
                    {
                        if ((m.Status == ContractMilestoneStatus.Planned
                             || m.Status == ContractMilestoneStatus.InProgress)
                            && m.PlannedDate <= DateTime.Now.AddDays(30))
                        {
                            UpcomingMilestones.Add(m);
                        }
                    }
                }

                ActiveProcedureCount = 0; // Stub: процедуры пока не выведены отдельным списком.
                StatusMessage = $"Загружено {PlanCount} планов, {Contracts.Count} контрактов, ближайших этапов: {UpcomingMilestones.Count}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка загрузки данных: {ex.Message}";
            }
        }
    }
}
