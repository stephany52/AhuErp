namespace AhuErp.Core.Models
{
    /// <summary>
    /// Статус этапа исполнения контракта.
    /// </summary>
    public enum ContractMilestoneStatus
    {
        Planned = 0,
        InProgress = 1,
        Completed = 2,
        Overdue = 3,
        Cancelled = 4,
    }
}
