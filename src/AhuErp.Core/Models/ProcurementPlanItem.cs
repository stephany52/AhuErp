using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Позиция плана закупок: один лот / один объект закупки. Содержит
    /// НМЦК, способ определения поставщика и плановый квартал размещения.
    /// </summary>
    public class ProcurementPlanItem
    {
        public int Id { get; set; }

        public int ProcurementPlanId { get; set; }
        public virtual ProcurementPlan ProcurementPlan { get; set; }

        /// <summary>Порядковый номер позиции в плане (для печати).</summary>
        public int LineNumber { get; set; }

        /// <summary>Код ОКПД2 (Общероссийский классификатор продукции по видам деятельности).</summary>
        [Required]
        [StringLength(32)]
        public string Okpd2Code { get; set; }

        /// <summary>Наименование объекта закупки.</summary>
        [Required]
        [StringLength(512)]
        public string Subject { get; set; }

        /// <summary>Начальная (максимальная) цена контракта, ₽.</summary>
        public decimal InitialMaxPrice { get; set; }

        public ProcurementMethod Method { get; set; } = ProcurementMethod.ElectronicAuction;

        public ProcurementQuarter PlannedQuarter { get; set; } = ProcurementQuarter.Q1;

        /// <summary>Источник финансирования (КБК / статья бюджета).</summary>
        [StringLength(128)]
        public string FundingSource { get; set; }

        [StringLength(2048)]
        public string Notes { get; set; }
    }
}
