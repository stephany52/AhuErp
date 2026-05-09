using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Путевой лист — бронирование транспортного средства на интервал времени.
    /// В Phase 4 обязательно привязан к документу-основанию (заявка на транспорт)
    /// и содержит ФИО водителя.
    /// </summary>
    public class VehicleTrip
    {
        public int Id { get; set; }

        public int VehicleId { get; set; }

        public virtual Vehicle Vehicle { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        /// <summary>
        /// Документ-основание (заявка на транспорт). Nullable на уровне БД
        /// для обратной совместимости с ранее созданными поездками; новый API
        /// бронирования в Phase 4 требует заполненного значения.
        /// </summary>
        public int? DocumentId { get; set; }

        public virtual Document Document { get; set; }

        [StringLength(128)]
        public string DriverName { get; set; }

        /// <summary>
        /// Документ-основание расширенный (Phase 7): прямая ссылка на
        /// зарегистрированный документ для отчётности и сквозного аудита.
        /// </summary>
        public int? BasisDocumentId { get; set; }

        public virtual Document BasisDocument { get; set; }

        // ---- Phase 15 / Improvement #12 — учёт ГСМ. ----

        /// <summary>Показания одометра на старте поездки (км).</summary>
        public int? OdometerStart { get; set; }

        /// <summary>Показания одометра по возвращении (км).</summary>
        public int? OdometerEnd { get; set; }

        /// <summary>Объём выданного топлива по путевому листу (литры).</summary>
        public decimal? FuelIssuedLiters { get; set; }

        /// <summary>
        /// Маршрут (текстовое описание: «Гараж — Центр МФЦ — Гараж»).
        /// </summary>
        [StringLength(512)]
        public string Route { get; set; }

        /// <summary>
        /// ФИО пассажиров для служебных перевозок должностных лиц,
        /// разделённые ';'. Используется при печати путевого листа.
        /// </summary>
        [StringLength(1024)]
        public string PassengerNames { get; set; }

        /// <summary>Фактическое время выезда (для журнала ГСМ).</summary>
        public DateTime? ActualStart { get; set; }

        /// <summary>Фактическое время возвращения (для журнала ГСМ).</summary>
        public DateTime? ActualEnd { get; set; }

        /// <summary>
        /// Расчётное использование топлива по одометру и норме расхода ТС.
        /// Возвращает <c>null</c>, если данных одометра / нормы недостаточно.
        /// </summary>
        public decimal? FuelUsedLiters
        {
            get
            {
                if (OdometerStart == null || OdometerEnd == null) return null;
                if (Vehicle == null || Vehicle.FuelConsumptionPer100Km <= 0) return null;
                var distanceKm = OdometerEnd.Value - OdometerStart.Value;
                if (distanceKm < 0) return null;
                return Math.Round(distanceKm * Vehicle.FuelConsumptionPer100Km / 100m, 2);
            }
        }

        /// <summary>Пройденное расстояние по одометру (км); <c>null</c>, если показания неполные.</summary>
        public int? DistanceKm =>
            OdometerStart.HasValue && OdometerEnd.HasValue
                ? OdometerEnd.Value - OdometerStart.Value
                : (int?)null;

        /// <summary>
        /// Интервалы пересекаются при выполнении условия Allen-overlap: start1 &lt; end2 и start2 &lt; end1.
        /// </summary>
        public bool OverlapsWith(DateTime otherStart, DateTime otherEnd)
        {
            return StartDate < otherEnd && otherStart < EndDate;
        }
    }
}
