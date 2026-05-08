using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Хронологическая запись в журнале диагностики ИТ-заявки. Phase 14:
    /// фиксирует шаги специалиста ИТО («Перезагрузил роутер» → «Заменил
    /// патч-корд» → «Передал в сервис»), что нужно для разбора инцидентов
    /// и расчёта MTTR.
    /// </summary>
    public class ItTicketDiagnosticEntry
    {
        public int Id { get; set; }

        public int TicketId { get; set; }
        public virtual ItTicket Ticket { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>Автор записи (специалист ИТО).</summary>
        public int AuthorId { get; set; }
        public virtual Employee Author { get; set; }

        [Required]
        [StringLength(1024)]
        public string Action { get; set; }

        /// <summary>
        /// Опциональная категория действия: «диагностика» / «ремонт» /
        /// «передача в сервис» / «закрытие». Для аналитических отчётов.
        /// </summary>
        [StringLength(64)]
        public string Category { get; set; }
    }
}
