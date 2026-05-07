namespace AhuErp.Core.Models
{
    /// <summary>
    /// Жизненный цикл документа в системе документооборота.
    /// </summary>
    public enum DocumentStatus
    {
        New = 0,
        InProgress = 1,
        OnHold = 2,
        Completed = 3,
        Cancelled = 4,
        Registered = 5
    }
}
