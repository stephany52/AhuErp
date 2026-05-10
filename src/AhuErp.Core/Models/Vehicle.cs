using System;
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

        // ---- Phase 17 / Improvement #14 — паспортные данные ТС, ОСАГО/ТО, путевой лист. ----

        /// <summary>
        /// Категория ТС для выбора печатной формы путевого листа: легковой
        /// (форма №3) / грузовой (форма №4-С) / автобус / спецтехника.
        /// </summary>
        public VehicleClass VehicleClass { get; set; } = VehicleClass.Passenger;

        /// <summary>Производитель/марка (Toyota, ГАЗ, ВАЗ).</summary>
        [StringLength(64)]
        public string Make { get; set; }

        /// <summary>Год выпуска (0 = не заполнено).</summary>
        public int Year { get; set; }

        /// <summary>VIN (17-значный), как в ПТС.</summary>
        [StringLength(32)]
        public string Vin { get; set; }

        /// <summary>Текущий пробег (показания одометра, км).</summary>
        public int? OdometerCurrent { get; set; }

        /// <summary>
        /// Пробег, при достижении которого требуется очередное ТО (км).
        /// Используется <see cref="Services.IVehicleMaintenanceService"/>.
        /// </summary>
        public int? NextMaintenanceOdometer { get; set; }

        /// <summary>Срок действия полиса ОСАГО.</summary>
        public DateTime? OsagoExpiry { get; set; }

        /// <summary>Срок действия диагностической карты / технического осмотра.</summary>
        public DateTime? TechInspectionExpiry { get; set; }

        public virtual ICollection<VehicleTrip> Trips { get; set; } = new HashSet<VehicleTrip>();
    }
}
