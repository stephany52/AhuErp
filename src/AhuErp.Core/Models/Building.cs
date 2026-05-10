using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Здание / корпус, эксплуатируемое МКУ «АХУ» БМР. Improvement #15 / Phase 18.
    /// Каркас для журналов эксплуатации и привязки помещений / основных средств:
    /// заявка на эксплуатационные работы (<see cref="MaintenanceRequest"/>) и
    /// карточка ОС (<see cref="FixedAsset"/>) могут ссылаться на здание напрямую
    /// либо через помещение (<see cref="Room"/>), что отражает реальную
    /// структуру учёта (двор / технический этаж — без помещения, кабинет —
    /// с помещением).
    /// </summary>
    public class Building
    {
        public int Id { get; set; }

        /// <summary>Краткое наименование («Главный корпус», «Гараж»).</summary>
        [Required]
        [StringLength(128)]
        public string Name { get; set; }

        /// <summary>Почтовый адрес.</summary>
        [StringLength(256)]
        public string Address { get; set; }

        /// <summary>Общая площадь, м². 0 — не заполнено.</summary>
        public decimal TotalAreaSqm { get; set; }

        /// <summary>Этажность.</summary>
        public int FloorCount { get; set; }

        /// <summary>Год ввода в эксплуатацию (0 — не заполнено).</summary>
        public int CommissionedYear { get; set; }

        /// <summary>Сотрудник, ответственный за эксплуатацию здания.</summary>
        public int? ResponsibleEmployeeId { get; set; }

        public virtual Employee ResponsibleEmployee { get; set; }

        [StringLength(2048)]
        public string Notes { get; set; }

        public virtual ICollection<Room> Rooms { get; set; } = new HashSet<Room>();
    }
}
