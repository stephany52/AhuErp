namespace AhuErp.Core.Models
{
    /// <summary>
    /// Жизненный цикл процедуры закупки. Применим ко всем способам
    /// определения поставщика; для единственного поставщика большинство
    /// промежуточных стадий пропускается (Planned → AwardedAndExecuted).
    /// </summary>
    public enum ProcurementProcedureStatus
    {
        Planned = 0,
        Announced = 1,
        BidsAccepted = 2,
        BidsEvaluation = 3,
        AwardedDecision = 4,
        AwardedAndExecuted = 5,
        Cancelled = 6,
        Failed = 7,
    }
}
