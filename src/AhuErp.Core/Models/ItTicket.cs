using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// IT-заявка Help Desk. Модель наследуется от <see cref="Document"/> через
    /// TPH-дискриминатор — это даёт единый Id-контур с остальными документами
    /// (включая связь с <see cref="InventoryTransaction.DocumentId"/>).
    /// </summary>
    /// <remarks>
    /// Phase 14 / Improvement #10: добавлены классификатор обращения
    /// <see cref="Kind"/>, ссылка на оборудование <see cref="AffectedEquipmentId"/>
    /// (с текстовым fallback'ом <see cref="AffectedEquipment"/> для миграции
    /// устаревших данных), журнал диагностики <see cref="DiagnosticEntries"/>
    /// и набор полей для передачи устройства в сервис: <see cref="VendorName"/>,
    /// <see cref="VendorTicketNumber"/>, <see cref="VendorReturnDeadline"/>,
    /// <see cref="IsSentToVendor"/>.
    /// </remarks>
    public class ItTicket : Document
    {
        /// <summary>
        /// Свободная строка — оборудование, описанное обращающимся пользователем
        /// при создании заявки (Phase 5). Сохранена как fallback для тикетов,
        /// у которых нет соответствующей записи в каталоге <see cref="Equipment"/>.
        /// </summary>
        [StringLength(256)]
        public string AffectedEquipment { get; set; }

        /// <summary>
        /// FK на каталог оборудования (Phase 14). Заполняется при создании
        /// заявки на конкретное устройство — позволяет историю заявок,
        /// группировку по сегментам и инв. номерам.
        /// </summary>
        public int? AffectedEquipmentId { get; set; }
        public virtual Equipment AffectedEquipmentRef { get; set; }

        /// <summary>Категория обращения (ремонт / ПО / сеть / ВКС / сайт / консультация).</summary>
        public ItTicketKind Kind { get; set; } = ItTicketKind.HardwareRepair;

        [StringLength(1024)]
        public string ResolutionNotes { get; set; }

        // ---- Phase 14: передача в сервис ------------------------------------

        /// <summary>
        /// Признак, что устройство в данный момент находится в стороннем
        /// сервисе. Обновляется одновременно с
        /// <see cref="EquipmentStatus.SentToVendor"/> у привязанного устройства.
        /// </summary>
        public bool IsSentToVendor { get; set; }

        [StringLength(256)]
        public string VendorName { get; set; }

        [StringLength(64)]
        public string VendorTicketNumber { get; set; }

        public DateTime? VendorReturnDeadline { get; set; }

        /// <summary>
        /// Дата фактического закрытия заявки. Устанавливается ИТО-сервисом
        /// при переводе тикета в <see cref="DocumentStatus.Completed"/> или
        /// <see cref="DocumentStatus.Cancelled"/>; используется для расчёта
        /// MTTR (Mean Time To Resolve) на дашборде ИТО.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        // ---- Phase 14: журнал диагностики -----------------------------------

        public virtual ICollection<ItTicketDiagnosticEntry> DiagnosticEntries { get; set; }
            = new HashSet<ItTicketDiagnosticEntry>();

        public ItTicket()
        {
            Type = DocumentType.It;
        }
    }
}
