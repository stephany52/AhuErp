using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Инвентаризационная процедура (ТМЦ, ОС, дел, помещений) с составом
    /// комиссии и выявленными расхождениями. Improvement #12 / Phase 15.
    /// </summary>
    public class Inventarization
    {
        public int Id { get; set; }

        public DateTime StartDate { get; set; }

        /// <summary>Дата окончания / подписания акта; nullable пока инвентаризация открыта.</summary>
        public DateTime? EndDate { get; set; }

        public InventarizationScope Scope { get; set; }

        /// <summary>Краткое наименование объекта инвентаризации (склад, корпус, отдел).</summary>
        [Required]
        [StringLength(256)]
        public string ScopeDescription { get; set; }

        /// <summary>
        /// Перечень членов комиссии, разделённых ';' — компактное представление
        /// для журнала; глубокая привязка к <see cref="Employee"/> сделана через
        /// отдельную сущность только при необходимости (минимизирует миграцию).
        /// </summary>
        [StringLength(2048)]
        public string CommissionMembers { get; set; }

        /// <summary>Председатель комиссии (FK на Employee).</summary>
        public int? ChairmanId { get; set; }

        public virtual Employee Chairman { get; set; }

        /// <summary>Документ-акт инвентаризации (FK на Document).</summary>
        public int? ResultDocumentId { get; set; }

        public virtual Document ResultDocument { get; set; }

        [StringLength(2048)]
        public string Notes { get; set; }

        public virtual ICollection<InventarizationDiscrepancy> Discrepancies { get; set; }
            = new HashSet<InventarizationDiscrepancy>();
    }
}
