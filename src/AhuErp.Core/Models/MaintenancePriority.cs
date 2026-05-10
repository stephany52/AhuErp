namespace AhuErp.Core.Models
{
    /// <summary>
    /// Приоритет эксплуатационной заявки. Improvement #15 / Phase 18. Влияет
    /// на сортировку очереди и SLA отчётности; <see cref="Urgent"/> зарезервирован
    /// под аварии (прорыв, отсутствие электричества).
    /// </summary>
    public enum MaintenancePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Urgent = 3,
    }
}
