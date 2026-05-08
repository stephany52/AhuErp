using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий записей журнала диагностики ИТ-заявки
    /// (Phase 14 / Improvement #10). Записи хранятся как дочерняя коллекция
    /// <see cref="ItTicket.DiagnosticEntries"/>; данный репозиторий
    /// упрощает добавление и выборку без явной загрузки тикета.
    /// </summary>
    public interface IItTicketDiagnosticRepository
    {
        ItTicketDiagnosticEntry Add(ItTicketDiagnosticEntry entry);
        IReadOnlyList<ItTicketDiagnosticEntry> ListByTicket(int ticketId);
    }
}
