using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Акт о выделении к уничтожению архивных документов, не подлежащих
    /// хранению (Improvement #16 / Phase 19). Соответствует приложению № 21
    /// к Правилам организации хранения … (Приказ Минкультуры от 31.03.2015 № 526)
    /// и Приказу Росархива от 20.12.2019 № 236.
    /// </summary>
    /// <remarks>
    /// Акт намеренно не наследует <see cref="Document"/>: это внутренний
    /// архивный документ, не проходящий обычный маршрут согласования / подписания
    /// и не попадающий в журналы РКК. Состав дел фиксируется снимком в
    /// <see cref="DestructionActItem"/> — даже если позже
    /// <see cref="NomenclatureCase"/> будет удалена, в акте сохраняется
    /// исторический индекс, заголовок, год и срок хранения.
    /// </remarks>
    public class DestructionAct
    {
        public int Id { get; set; }

        /// <summary>Регистрационный номер акта (например, «АКТ-2026-001»).</summary>
        [Required]
        [StringLength(64)]
        public string ActNumber { get; set; }

        /// <summary>Дата составления акта.</summary>
        public DateTime ActDate { get; set; }

        /// <summary>Состояние акта (проект → утверждён → исполнен).</summary>
        public DestructionStatus Status { get; set; } = DestructionStatus.Draft;

        /// <summary>Сотрудник, составивший проект акта (архивариус).</summary>
        public int DraftedByEmployeeId { get; set; }
        public virtual Employee DraftedByEmployee { get; set; }

        /// <summary>Сотрудник, утвердивший акт (руководитель / зам по АХЧ).</summary>
        public int? ApprovedByEmployeeId { get; set; }
        public virtual Employee ApprovedByEmployee { get; set; }

        /// <summary>Дата утверждения (фиксируется в момент перевода в <see cref="DestructionStatus.Approved"/>).</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>Дата физического уничтожения (фиксируется в <see cref="DestructionStatus.Executed"/>).</summary>
        public DateTime? ExecutedAt { get; set; }

        /// <summary>Способ уничтожения (шредер / сжигание / промышленная переработка).</summary>
        [StringLength(256)]
        public string DestructionMethod { get; set; }

        /// <summary>Заметки (мотивированное обоснование, ссылки на протокол ЭПК).</summary>
        [StringLength(4096)]
        public string Notes { get; set; }

        public virtual ICollection<DestructionActItem> Items { get; set; }
            = new HashSet<DestructionActItem>();
    }
}
