using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Журнал видеоконференций — Phase 14 / Improvement #10. Должностная
    /// инструкция системного администратора (info.txt) включает
    /// «проведение и настройку видеоконференций»; в текущем коде учёта
    /// ВКС не было вообще. Запись сохраняет тему, дату, организатора,
    /// участников и площадку, чтобы в дальнейшем поднимать историю и
    /// прикладывать к отчёту ИТО.
    /// </summary>
    public class VideoConference
    {
        public int Id { get; set; }

        /// <summary>Опциональная связь с подготавливающей ИТ-заявкой.</summary>
        public int? TicketId { get; set; }
        public virtual ItTicket Ticket { get; set; }

        [Required]
        [StringLength(256)]
        public string Topic { get; set; }

        public DateTime ScheduledAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int OrganizerId { get; set; }
        public virtual Employee Organizer { get; set; }

        /// <summary>Список участников (свободная строка / список ФИО, разделитель — перенос строки).</summary>
        [StringLength(2048)]
        public string Participants { get; set; }

        public VideoConferencePlatform Platform { get; set; } = VideoConferencePlatform.RegionalVks;

        [StringLength(1024)]
        public string MeetingUrl { get; set; }

        [StringLength(512)]
        public string Notes { get; set; }
    }
}
