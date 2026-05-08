using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Сегмент локальной сети учреждения — VLAN/IP-диапазон с настройками
    /// шлюза, маски и DNS. К сегменту привязывается оборудование
    /// (<see cref="Equipment.NetworkSegmentId"/>), что позволяет
    /// специалисту ИТО быстро находить устройства одного сегмента
    /// при поиске неисправности (info.txt: «настройка сети»).
    /// </summary>
    public class NetworkSegment
    {
        public int Id { get; set; }

        [Required]
        [StringLength(128)]
        public string Name { get; set; }

        /// <summary>VLAN ID (например, «10», «20»). Опционально.</summary>
        [StringLength(16)]
        public string Vlan { get; set; }

        /// <summary>CIDR-нотация диапазона: «192.168.10.0/24».</summary>
        [StringLength(32)]
        public string IpRange { get; set; }

        [StringLength(32)]
        public string SubnetMask { get; set; }

        [StringLength(32)]
        public string Gateway { get; set; }

        /// <summary>Список DNS, разделитель — запятая.</summary>
        [StringLength(128)]
        public string Dns { get; set; }

        [StringLength(512)]
        public string Notes { get; set; }

        public virtual ICollection<Equipment> Equipment { get; set; } = new HashSet<Equipment>();
    }
}
