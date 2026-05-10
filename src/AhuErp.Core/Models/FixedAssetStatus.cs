namespace AhuErp.Core.Models
{
    /// <summary>
    /// Жизненный цикл основного средства. Improvement #15 / Phase 18. Намеренно
    /// независим от <see cref="EquipmentStatus"/>: ОС учитывается бухгалтерией,
    /// статус «<see cref="Decommissioned"/>» подразумевает наличие акта списания
    /// и не возвращается в эксплуатацию автоматически.
    /// </summary>
    public enum FixedAssetStatus
    {
        /// <summary>В эксплуатации.</summary>
        InUse = 0,

        /// <summary>На складе / в резерве.</summary>
        InStock = 1,

        /// <summary>В ремонте.</summary>
        InRepair = 2,

        /// <summary>Списано.</summary>
        Decommissioned = 3,
    }
}
