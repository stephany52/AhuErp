using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// План-график муниципальных закупок учреждения на финансовый год
    /// согласно ст. 16 Федерального закона от 05.04.2013 № 44-ФЗ.
    /// Содержит набор позиций (<see cref="ProcurementPlanItem"/>) с НМЦК,
    /// способом определения поставщика и плановым кварталом размещения.
    /// </summary>
    public class ProcurementPlan
    {
        public int Id { get; set; }

        /// <summary>Финансовый год плана (например, 2026).</summary>
        public int Year { get; set; }

        [Required]
        [StringLength(256)]
        public string Title { get; set; }

        public ProcurementPlanStatus Status { get; set; } = ProcurementPlanStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedByEmployeeId { get; set; }

        public virtual Employee ApprovedByEmployee { get; set; }

        public DateTime? PublishedAt { get; set; }

        /// <summary>Идентификатор плана в ЕИС (zakupki.gov.ru), если опубликован.</summary>
        [StringLength(64)]
        public string EisRegistrationNumber { get; set; }

        [StringLength(2048)]
        public string Notes { get; set; }

        public virtual ICollection<ProcurementPlanItem> Items { get; set; }
            = new HashSet<ProcurementPlanItem>();
    }
}
