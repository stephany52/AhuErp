using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий контрактов (Phase 20 / Improvement #13). Контракт хранится
    /// в TPH-таблице <c>Documents</c>; репозиторий инкапсулирует фильтр по
    /// дискриминатору и предоставляет API для этапов исполнения.
    /// </summary>
    public interface IContractRepository
    {
        Contract Add(Contract contract);
        Contract Update(Contract contract);
        Contract Get(int id);
        IReadOnlyList<Contract> List();
        IReadOnlyList<Contract> ListByProcedure(int procedureId);

        ContractMilestone AddMilestone(ContractMilestone milestone);
        ContractMilestone UpdateMilestone(ContractMilestone milestone);
        ContractMilestone GetMilestone(int milestoneId);
        IReadOnlyList<ContractMilestone> ListMilestones(int contractId);

        /// <summary>
        /// Все этапы по всем контрактам с плановой датой в диапазоне
        /// [<paramref name="from"/>; <paramref name="to"/>] и статусом
        /// <see cref="ContractMilestoneStatus.Planned"/> или
        /// <see cref="ContractMilestoneStatus.InProgress"/>. Используется
        /// <see cref="IProcurementService.TickMilestoneReminders"/>.
        /// </summary>
        IReadOnlyList<ContractMilestone> ListUpcomingMilestones(System.DateTime from, System.DateTime to);
    }
}
