using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Запись журнала передачи дел в архив. Improvement #12 / Phase 15.
    /// Связывает номенклатурное дело с актом приёма-передачи и фиксирует
    /// присвоенный архивный шифр.
    /// </summary>
    public class ArchiveTransfer
    {
        public int Id { get; set; }

        public int NomenclatureCaseId { get; set; }

        public virtual NomenclatureCase NomenclatureCase { get; set; }

        public DateTime TransferDate { get; set; }

        /// <summary>Сотрудник-передающий (делопроизводитель / руководитель отдела).</summary>
        public int? TransferredById { get; set; }

        public virtual Employee TransferredBy { get; set; }

        /// <summary>Сотрудник-принимающий (архивист).</summary>
        public int? AcceptedById { get; set; }

        public virtual Employee AcceptedBy { get; set; }

        /// <summary>Документ-акт приёма-передачи (FK на Document).</summary>
        public int? ActDocumentId { get; set; }

        public virtual Document ActDocument { get; set; }

        /// <summary>Архивный шифр, присвоенный делу после приёма.</summary>
        [StringLength(64)]
        public string ArchiveCode { get; set; }

        /// <summary>Срок хранения, лет (копируется из <see cref="DocumentTypeRef.DefaultRetentionYears"/>).</summary>
        public int RetentionYears { get; set; }

        [StringLength(2048)]
        public string Notes { get; set; }
    }
}
