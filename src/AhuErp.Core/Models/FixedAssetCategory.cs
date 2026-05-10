namespace AhuErp.Core.Models
{
    /// <summary>
    /// Категория основного средства. Improvement #15 / Phase 18. Используется
    /// для группировки в реестре ОС и для фильтрации при инвентаризации
    /// (<see cref="Inventarization"/> с <see cref="InventarizationScope.FixedAssets"/>).
    /// </summary>
    public enum FixedAssetCategory
    {
        /// <summary>Прочее.</summary>
        Other = 0,

        /// <summary>Оргтехника / ПК / периферия.</summary>
        OfficeEquipment = 1,

        /// <summary>Мебель.</summary>
        Furniture = 2,

        /// <summary>Транспорт (привязывается также к <see cref="Vehicle"/>).</summary>
        Vehicle = 3,

        /// <summary>Здание / сооружение.</summary>
        Building = 4,

        /// <summary>Инвентарь хозяйственного назначения.</summary>
        HouseholdInventory = 5,

        /// <summary>Сетевое / серверное оборудование.</summary>
        NetworkEquipment = 6,
    }
}
