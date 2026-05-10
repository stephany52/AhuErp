using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Карточка основного средства (ОС). Improvement #15 / Phase 18. Намеренно
    /// отдельная сущность от <see cref="Equipment"/>: <c>Equipment</c> — ИТ-каталог
    /// с акцентом на сетевые реквизиты и заявки ИТО, <c>FixedAsset</c> — учётный
    /// реестр бухгалтерии (балансовая стоимость, инвентарный номер по форме ОС-6,
    /// привязка к материально ответственному лицу). Один и тот же физический
    /// объект может присутствовать в обоих реестрах с разными ролями.
    /// </summary>
    public class FixedAsset
    {
        public int Id { get; set; }

        /// <summary>Бухгалтерский инвентарный номер (форма ОС-6).</summary>
        [Required]
        [StringLength(64)]
        public string InventoryNumber { get; set; }

        [Required]
        [StringLength(256)]
        public string Name { get; set; }

        public FixedAssetCategory Category { get; set; } = FixedAssetCategory.Other;

        public FixedAssetStatus Status { get; set; } = FixedAssetStatus.InUse;

        /// <summary>Дата принятия к учёту.</summary>
        public DateTime? AcquisitionDate { get; set; }

        /// <summary>Первоначальная (балансовая) стоимость.</summary>
        public decimal AcquisitionCost { get; set; }

        /// <summary>Остаточная стоимость с учётом амортизации.</summary>
        public decimal BookValue { get; set; }

        /// <summary>Здание, в котором находится ОС (опционально).</summary>
        public int? BuildingId { get; set; }
        public virtual Building Building { get; set; }

        /// <summary>Помещение (опционально, если требуется точная локализация).</summary>
        public int? RoomId { get; set; }
        public virtual Room Room { get; set; }

        /// <summary>Материально ответственное лицо (МОЛ).</summary>
        public int? ResponsibleEmployeeId { get; set; }
        public virtual Employee ResponsibleEmployee { get; set; }

        /// <summary>Дата списания (заполняется при <see cref="FixedAssetStatus.Decommissioned"/>).</summary>
        public DateTime? DecommissionedAt { get; set; }

        /// <summary>Документ-акт списания (FK на <see cref="Document"/>), опционально.</summary>
        public int? DecommissionDocumentId { get; set; }
        public virtual Document DecommissionDocument { get; set; }

        [StringLength(2048)]
        public string Notes { get; set; }
    }
}
