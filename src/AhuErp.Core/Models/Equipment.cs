using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Каталог оборудования ИТО — Phase 14 / Improvement #10. До этой фазы
    /// поле <see cref="ItTicket.AffectedEquipment"/> было свободной строкой,
    /// что не давало вести учёт по инвентарным номерам, привязывать
    /// оборудование к кабинету / сетевому сегменту и видеть историю заявок
    /// по конкретной единице.
    /// </summary>
    /// <remarks>
    /// Разделение Equipment ↔ ItTicket: одна заявка может затрагивать одну
    /// единицу оборудования (<see cref="ItTicket.AffectedEquipmentId"/>).
    /// Старое строковое поле сохраняется как fallback на случай, если
    /// устройство не заведено в каталог (миграция данных Phase 14 не
    /// принудительно перекладывает строки в FK).
    /// </remarks>
    public class Equipment
    {
        public int Id { get; set; }

        /// <summary>Инвентарный номер (бухгалтерский / ИТО), уникален в пределах учреждения.</summary>
        [Required]
        [StringLength(64)]
        public string InventoryNumber { get; set; }

        public EquipmentType Type { get; set; } = EquipmentType.Other;

        /// <summary>Производитель и модель: «TP-Link EAP245», «HP LaserJet M404».</summary>
        [StringLength(256)]
        public string Model { get; set; }

        [StringLength(64)]
        public string SerialNumber { get; set; }

        [StringLength(32)]
        public string MacAddress { get; set; }

        [StringLength(32)]
        public string IpAddress { get; set; }

        /// <summary>Кабинет / помещение, где установлено оборудование.</summary>
        [StringLength(64)]
        public string Room { get; set; }

        public int? ResponsibleEmployeeId { get; set; }
        public virtual Employee ResponsibleEmployee { get; set; }

        /// <summary>Дата ввода в эксплуатацию.</summary>
        public DateTime? InServiceDate { get; set; }

        /// <summary>Окончание гарантии.</summary>
        public DateTime? WarrantyExpiry { get; set; }

        public EquipmentStatus Status { get; set; } = EquipmentStatus.Working;

        /// <summary>Сегмент сети, к которому подключено оборудование (опционально).</summary>
        public int? NetworkSegmentId { get; set; }
        public virtual NetworkSegment NetworkSegment { get; set; }

        [StringLength(1024)]
        public string Notes { get; set; }

        public virtual ICollection<ItTicket> Tickets { get; set; } = new HashSet<ItTicket>();
    }
}
