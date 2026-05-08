using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация поставщика KPI-метрик ИТО, работающая поверх
    /// <see cref="IDocumentRepository.ListItTickets"/>. Вынесена в core
    /// (а не в UI), чтобы было удобно покрыть unit-тестами и переиспользовать
    /// в отчётах.
    /// </summary>
    /// <remarks>
    /// Определения:
    /// <list type="bullet">
    /// <item>Open — заявка не в терминальном статусе (не Completed / не Cancelled).</item>
    /// <item>InProgress — заявка взята в работу: <see cref="DocumentStatus.InProgress"/>
    /// либо передана в сервис (флаг <see cref="ItTicket.IsSentToVendor"/>).</item>
    /// <item>Overdue — открытая заявка с дедлайном, который уже наступил.</item>
    /// <item>SentToVendor — открытая заявка с активной передачей в сервис.</item>
    /// <item>MTTR — средняя длительность от <see cref="Document.CreationDate"/>
    /// до <see cref="ItTicket.CompletedAt"/> по закрытым заявкам. Заявки без
    /// заполненного <c>CompletedAt</c> в расчёт MTTR не попадают, чтобы не
    /// искажать среднее значениями «по умолчанию».</item>
    /// </list>
    /// </remarks>
    public sealed class ItServiceMetricsProvider : IItServiceMetricsProvider
    {
        private readonly IDocumentRepository _documents;

        public ItServiceMetricsProvider(IDocumentRepository documents)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        }

        public ItServiceMetrics Compute(DateTime? asOf = null)
        {
            var now = asOf ?? DateTime.Now;

            IReadOnlyList<ItTicket> tickets = _documents.ListItTickets();
            if (tickets == null || tickets.Count == 0)
                return ItServiceMetrics.Empty;

            int openCount = 0;
            int inProgressCount = 0;
            int overdueCount = 0;
            int sentToVendorCount = 0;
            int completedCount = 0;

            var resolutionDurations = new List<TimeSpan>();

            foreach (var t in tickets)
            {
                if (IsTerminal(t.Status))
                {
                    if (t.Status == DocumentStatus.Completed)
                    {
                        completedCount++;
                        if (t.CompletedAt.HasValue)
                        {
                            var duration = t.CompletedAt.Value - t.CreationDate;
                            if (duration > TimeSpan.Zero)
                                resolutionDurations.Add(duration);
                        }
                    }

                    continue;
                }

                openCount++;

                if (t.IsSentToVendor)
                {
                    sentToVendorCount++;
                    inProgressCount++;
                }
                else if (t.Status == DocumentStatus.InProgress)
                {
                    inProgressCount++;
                }

                if (t.Deadline != default(DateTime) && t.Deadline < now)
                    overdueCount++;
            }

            TimeSpan? mttr = null;
            if (resolutionDurations.Count > 0)
            {
                var totalTicks = resolutionDurations.Sum(d => d.Ticks);
                mttr = TimeSpan.FromTicks(totalTicks / resolutionDurations.Count);
            }

            return new ItServiceMetrics(
                openCount,
                inProgressCount,
                overdueCount,
                sentToVendorCount,
                completedCount,
                mttr);
        }

        private static bool IsTerminal(DocumentStatus status)
            => status == DocumentStatus.Completed
            || status == DocumentStatus.Cancelled;
    }
}
