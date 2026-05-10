namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 17 / Improvement #14 — категория ТС для выбора печатной формы
    /// путевого листа.
    /// <list type="bullet">
    ///   <item><description><see cref="Passenger"/> — легковой автомобиль,
    ///     путевой лист по форме №3 (Постановление Госкомстата от 28.11.1997 №78).</description></item>
    ///   <item><description><see cref="Truck"/> — грузовой автомобиль,
    ///     путевой лист по форме №4-С (сдельная) для повременной/сдельной перевозки.</description></item>
    ///   <item><description><see cref="Bus"/> — автобус, путевой лист
    ///     автобуса (форма №6 / №6-СПЕЦ для необщего пользования).</description></item>
    ///   <item><description><see cref="Special"/> — специальная техника
    ///     (эвакуатор, погрузчик), используется обобщённая форма автомобиля.</description></item>
    /// </list>
    /// </summary>
    public enum VehicleClass
    {
        Passenger = 0,
        Truck = 1,
        Bus = 2,
        Special = 3,
    }
}
