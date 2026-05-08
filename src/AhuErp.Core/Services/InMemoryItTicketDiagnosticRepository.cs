using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация <see cref="IItTicketDiagnosticRepository"/>.</summary>
    public sealed class InMemoryItTicketDiagnosticRepository : IItTicketDiagnosticRepository
    {
        private readonly List<ItTicketDiagnosticEntry> _items = new List<ItTicketDiagnosticEntry>();
        private int _nextId = 1;

        public ItTicketDiagnosticEntry Add(ItTicketDiagnosticEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry.TicketId <= 0)
                throw new ArgumentException("Не указан тикет.", nameof(entry));
            if (string.IsNullOrWhiteSpace(entry.Action))
                throw new ArgumentException("Описание действия обязательно.", nameof(entry));

            if (entry.Id == 0) entry.Id = _nextId++;
            else _nextId = Math.Max(_nextId, entry.Id + 1);

            _items.Add(entry);
            return entry;
        }

        public IReadOnlyList<ItTicketDiagnosticEntry> ListByTicket(int ticketId)
            => _items.Where(e => e.TicketId == ticketId)
                .OrderBy(e => e.Timestamp)
                .ThenBy(e => e.Id)
                .ToList()
                .AsReadOnly();
    }
}
