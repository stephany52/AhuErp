using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="IItTicketDiagnosticRepository"/>.</summary>
    public sealed class EfItTicketDiagnosticRepository : IItTicketDiagnosticRepository
    {
        private readonly AhuDbContext _ctx;

        public EfItTicketDiagnosticRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public ItTicketDiagnosticEntry Add(ItTicketDiagnosticEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry.TicketId <= 0)
                throw new ArgumentException("Не указан тикет.", nameof(entry));
            if (string.IsNullOrWhiteSpace(entry.Action))
                throw new ArgumentException("Описание действия обязательно.", nameof(entry));

            _ctx.ItTicketDiagnosticEntries.Add(entry);
            _ctx.SaveChanges();
            return entry;
        }

        public IReadOnlyList<ItTicketDiagnosticEntry> ListByTicket(int ticketId)
            => _ctx.ItTicketDiagnosticEntries
                .Where(e => e.TicketId == ticketId)
                .OrderBy(e => e.Timestamp)
                .ThenBy(e => e.Id)
                .ToList()
                .AsReadOnly();
    }
}
