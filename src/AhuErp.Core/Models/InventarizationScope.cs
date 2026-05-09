namespace AhuErp.Core.Models
{
    /// <summary>
    /// Объект инвентаризации: склад ТМЦ, основные средства,
    /// номенклатура дел, помещения и т.п.
    /// </summary>
    public enum InventarizationScope
    {
        Inventory = 0,
        FixedAssets = 1,
        Documents = 2,
        Premises = 3,
        Vehicles = 4,
        Other = 99
    }
}
