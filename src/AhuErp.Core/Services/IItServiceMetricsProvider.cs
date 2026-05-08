using System;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Поставщик KPI-метрик ИТО (Phase 14 / Improvement #10): открытые
    /// заявки, в работе, просрочено, средний MTTR. Используется в
    /// дашборде <c>ItServiceView</c> и потенциально — в отчётах.
    /// </summary>
    public interface IItServiceMetricsProvider
    {
        ItServiceMetrics Compute(DateTime? asOf = null);
    }

    /// <summary>
    /// Снимок KPI-метрик ИТ-службы. <see cref="MeanTimeToResolve"/> равен
    /// <c>null</c>, если за расчётный период не было ни одной закрытой заявки.
    /// </summary>
    public sealed class ItServiceMetrics
    {
        public int OpenCount { get; }
        public int InProgressCount { get; }
        public int OverdueCount { get; }
        public int SentToVendorCount { get; }
        public int CompletedCount { get; }
        public TimeSpan? MeanTimeToResolve { get; }

        public ItServiceMetrics(
            int openCount,
            int inProgressCount,
            int overdueCount,
            int sentToVendorCount,
            int completedCount,
            TimeSpan? meanTimeToResolve)
        {
            OpenCount = openCount;
            InProgressCount = inProgressCount;
            OverdueCount = overdueCount;
            SentToVendorCount = sentToVendorCount;
            CompletedCount = completedCount;
            MeanTimeToResolve = meanTimeToResolve;
        }

        public static ItServiceMetrics Empty { get; }
            = new ItServiceMetrics(0, 0, 0, 0, 0, null);
    }
}
