namespace AhuErp.Core.Models
{
    /// <summary>
    /// Жизненный цикл плана-графика закупок (Phase 20 / Improvement #13):
    /// <c>Draft</c> → <c>Approved</c> → <c>Published</c> → <c>Closed</c>.
    /// <c>Closed</c> — терминальный; «Отменён» отдельно не выделен, чтобы
    /// не плодить хвосты при импорте из ЕИС.
    /// </summary>
    public enum ProcurementPlanStatus
    {
        Draft = 0,
        Approved = 1,
        Published = 2,
        Closed = 3,
    }
}
