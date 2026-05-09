using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 16 / Improvement #17 — журнал попыток входа.
    /// Запись только append-only; модификация/удаление не предусмотрены.
    /// </summary>
    public interface ILoginAttemptRepository
    {
        LoginAttempt Add(LoginAttempt attempt);

        /// <summary>
        /// Количество неудачных попыток для сотрудника, попавших в окно
        /// (<paramref name="fromUtc"/>, сейчас]. Используется для проверки
        /// порога блокировки.
        /// </summary>
        int CountRecentFailures(int employeeId, DateTime fromUtc);

        /// <summary>
        /// Возвращает последние <paramref name="limit"/> записей по
        /// убыванию времени. Опциональный фильтр по сотруднику и/или
        /// диапазону дат (для админ-панели).
        /// </summary>
        IReadOnlyList<LoginAttempt> Query(int? employeeId, DateTime? fromUtc, DateTime? toUtc, int limit);
    }
}
