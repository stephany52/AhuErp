using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Процедура определения поставщика по конкретной позиции плана-графика.
    /// Способ закупки фиксируется из <see cref="ProcurementPlanItem.Method"/>,
    /// но может быть переопределён при размещении (например, переход на
    /// единственного поставщика при несостоявшейся процедуре).
    /// </summary>
    public class ProcurementProcedure
    {
        public int Id { get; set; }

        public int ProcurementPlanItemId { get; set; }
        public virtual ProcurementPlanItem ProcurementPlanItem { get; set; }

        /// <summary>Извещение в ЕИС (zakupki.gov.ru), регистрационный номер.</summary>
        [StringLength(64)]
        public string EisNoticeNumber { get; set; }

        public ProcurementMethod Method { get; set; } = ProcurementMethod.ElectronicAuction;

        public ProcurementProcedureStatus Status { get; set; } = ProcurementProcedureStatus.Planned;

        public DateTime? AnnouncedAt { get; set; }

        public DateTime? BidsDeadline { get; set; }

        public DateTime? AwardDecisionAt { get; set; }

        /// <summary>ИНН победителя (поставщика).</summary>
        [StringLength(32)]
        public string AwardedSupplierInn { get; set; }

        /// <summary>Наименование победителя (поставщика).</summary>
        [StringLength(512)]
        public string AwardedSupplierName { get; set; }

        /// <summary>Цена контракта по итогам процедуры, ₽.</summary>
        public decimal? AwardedPrice { get; set; }

        [StringLength(2048)]
        public string Notes { get; set; }
    }
}
