using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Транспортное средство автопарка учреждения.
    /// </summary>
    public class Vehicle
    {
        public int Id { get; set; }

        [Required]
        [StringLength(128)]
        public string Model { get; set; }

        [Required]
        [StringLength(32)]
        public string LicensePlate { get; set; }

        public VehicleStatus CurrentStatus { get; set; } = VehicleStatus.Available;

        // ---- Phase 15 / Improvement #12 — учёт ГСМ. ----

        /// <summary>Тип топлива (бензин/дизель/газ/электро/гибрид).</summary>
        public FuelType FuelType { get; set; } = FuelType.Petrol;

        /// <summary>
        /// Норма расхода топлива на 100 км пробега, в литрах. Используется
        /// для автоматического расчёта <see cref="VehicleTrip.FuelUsedLiters"/>
        /// при наличии данных одометра.
        /// </summary>
        public decimal FuelConsumptionPer100Km { get; set; }

        public virtual ICollection<VehicleTrip> Trips { get; set; } = new HashSet<VehicleTrip>();
    }
}
