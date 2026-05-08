namespace AhuErp.Core.Models
{
    /// <summary>
    /// Жизненный цикл единицы оборудования. Управляется ИТО-специалистом
    /// (Bug #6 / Improvement #9 — TechSupport, Admin), все переходы
    /// сопровождаются аудит-записями.
    /// </summary>
    /// <remarks>
    /// Целочисленные значения зафиксированы — менять нельзя, чтобы не сломать
    /// сохранённые значения в БД.
    /// </remarks>
    public enum EquipmentStatus
    {
        /// <summary>В рабочем состоянии, в эксплуатации.</summary>
        Working = 0,

        /// <summary>Сейчас в ремонте (на месте, силами ИТО).</summary>
        InRepair = 1,

        /// <summary>Передано в сервис стороннему поставщику.</summary>
        SentToVendor = 2,

        /// <summary>Списано — снято с эксплуатации.</summary>
        Decommissioned = 3,

        /// <summary>Резерв / на складе ИТО, готово к выдаче.</summary>
        InReserve = 4
    }
}
