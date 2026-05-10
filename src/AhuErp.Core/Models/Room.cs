using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Помещение внутри здания (<see cref="Building"/>). Improvement #15 / Phase 18.
    /// Уникальность номера обеспечивается на уровне репозитория в пределах одного
    /// здания (а не глобально), потому что в разных корпусах могут быть кабинеты
    /// с одинаковыми номерами («101» в главном и в гараже).
    /// </summary>
    public class Room
    {
        public int Id { get; set; }

        public int BuildingId { get; set; }
        public virtual Building Building { get; set; }

        /// <summary>Номер / литер помещения («101», «1Б», «подвал-3»).</summary>
        [Required]
        [StringLength(32)]
        public string Number { get; set; }

        /// <summary>Этаж (отрицательные значения — подвал/цоколь).</summary>
        public int Floor { get; set; }

        /// <summary>Площадь, м². 0 — не заполнено.</summary>
        public decimal AreaSqm { get; set; }

        public RoomPurpose Purpose { get; set; } = RoomPurpose.Office;

        /// <summary>Сотрудник, ответственный за помещение (комендант / арендатор).</summary>
        public int? ResponsibleEmployeeId { get; set; }

        public virtual Employee ResponsibleEmployee { get; set; }

        [StringLength(1024)]
        public string Notes { get; set; }
    }
}
