namespace AhuErp.Core.Models
{
    /// <summary>
    /// Статус эксплуатационной заявки. Improvement #15 / Phase 18. Намеренно
    /// независим от <see cref="DocumentStatus"/>: эксплуатационная заявка не
    /// проходит маршрут согласований/подписей и живёт в простом конечном
    /// автомате <c>Open → InProgress → Completed | Cancelled</c>.
    /// </summary>
    public enum MaintenanceStatus
    {
        /// <summary>Принята, ожидает исполнителя.</summary>
        Open = 0,

        /// <summary>Назначен исполнитель, работы идут.</summary>
        InProgress = 1,

        /// <summary>Работы выполнены, заявка закрыта.</summary>
        Completed = 2,

        /// <summary>Отменена (отказ заявителя / неактуально).</summary>
        Cancelled = 3,
    }
}
