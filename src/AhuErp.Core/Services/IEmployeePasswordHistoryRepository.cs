using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 16 / Improvement #17 — история паролей сотрудника. Используется
    /// <see cref="IPasswordPolicy"/> для запрета повторного использования
    /// последних N паролей. Запись добавляется при каждой успешной смене
    /// пароля; при превышении лимита самые старые записи могут быть удалены
    /// (rotation).
    /// </summary>
    public interface IEmployeePasswordHistoryRepository
    {
        /// <summary>Возвращает историю паролей сотрудника по убыванию даты.</summary>
        IReadOnlyList<EmployeePasswordHistory> ListForEmployee(int employeeId);

        /// <summary>Добавляет новую запись истории.</summary>
        EmployeePasswordHistory Add(EmployeePasswordHistory entry);

        /// <summary>
        /// Оставляет только <paramref name="depth"/> самых свежих записей
        /// для сотрудника, остальные удаляет. Не критично к гонкам — при
        /// логине берётся top N свежих по времени.
        /// </summary>
        void TrimToDepth(int employeeId, int depth);
    }
}
