using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Заявка на эксплуатационные работы (электрика / сантехника / уборка / ремонт).
    /// Improvement #15 / Phase 18. Намеренно отдельная сущность от <see cref="Document"/>:
    /// большинство таких заявок — операционные «тикеты» хозяйственного отдела, не
    /// проходят согласование / подпись и не должны засорять журнал РКК. Поле
    /// <see cref="LinkedDocumentId"/> позволяет привязать заявку к РКК (например,
    /// служебная записка инициатора), если это требуется для аудита или сметы.
    /// </summary>
    public class MaintenanceRequest
    {
        public int Id { get; set; }

        public DateTime RegistrationDate { get; set; }

        public int BuildingId { get; set; }
        public virtual Building Building { get; set; }

        /// <summary>Помещение (опционально — авария на крыше / во дворе).</summary>
        public int? RoomId { get; set; }
        public virtual Room Room { get; set; }

        /// <summary>Сотрудник, подавший заявку.</summary>
        public int RequesterEmployeeId { get; set; }
        public virtual Employee RequesterEmployee { get; set; }

        public MaintenanceKind Kind { get; set; } = MaintenanceKind.Other;

        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Normal;

        public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;

        [Required]
        [StringLength(2048)]
        public string Description { get; set; }

        /// <summary>Назначенный исполнитель (электрик/сантехник/хозяйственник).</summary>
        public int? AssigneeEmployeeId { get; set; }
        public virtual Employee AssigneeEmployee { get; set; }

        /// <summary>Дата фактического завершения (заполняется при переводе в <see cref="MaintenanceStatus.Completed"/>).</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Описание выполненных работ / причины отмены.</summary>
        [StringLength(2048)]
        public string Resolution { get; set; }

        /// <summary>FK на связанную РКК (служебная записка инициатора), если есть.</summary>
        public int? LinkedDocumentId { get; set; }
        public virtual Document LinkedDocument { get; set; }
    }
}
