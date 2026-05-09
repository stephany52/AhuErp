namespace AhuErp.Core.Models
{
    /// <summary>
    /// Тип топлива транспортного средства. Используется журналом учёта ГСМ
    /// (Improvement #12 / Phase 15) для фильтрации и расчёта.
    /// </summary>
    public enum FuelType
    {
        Petrol = 0,
        Diesel = 1,
        Gas = 2,
        Electric = 3,
        Hybrid = 4
    }
}
