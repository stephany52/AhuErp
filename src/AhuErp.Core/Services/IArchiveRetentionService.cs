using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис управления сроками хранения архивных дел и оформления актов
    /// о выделении документов к уничтожению (Improvement #16 / Phase 19).
    /// </summary>
    /// <remarks>
    /// Сервис работает с двумя источниками срока хранения:
    /// <list type="bullet">
    ///   <item><description><see cref="NomenclatureCase.RetentionPeriodYears"/> —
    ///     заданный архивариусом срок для дела целиком (0 — постоянное хранение,
    ///     в акт уничтожения попасть не может).</description></item>
    ///   <item><description><see cref="DocumentTypeRef.DefaultRetentionYears"/> —
    ///     значение по умолчанию для вида документа, используется как fallback,
    ///     если дело не имеет собственного срока (<c>RetentionPeriodYears = -1</c>
    ///     не используется — отрицательные значения трактуются как «не задано»).</description></item>
    /// </list>
    /// Решение об уничтожении принимает экспертно-проверочная комиссия (ЭПК),
    /// сервис лишь формирует кандидатов и фиксирует результат.
    /// </remarks>
    public interface IArchiveRetentionService
    {
        /// <summary>
        /// Возвращает дела, у которых истёк срок хранения по состоянию на <paramref name="asOf"/>.
        /// </summary>
        /// <param name="asOf">Логическая дата отсчёта (обычно <see cref="DateTime.Today"/>).</param>
        /// <returns>Список дел, отсортированных по году и индексу.</returns>
        IReadOnlyList<NomenclatureCase> FindEligibleForDestruction(DateTime asOf);

        /// <summary>
        /// Создаёт проект акта о выделении к уничтожению. Состав строк формируется
        /// снимком: индекс, заголовок, год, срок хранения копируются из дел.
        /// </summary>
        /// <param name="actNumber">Регистрационный номер акта.</param>
        /// <param name="actDate">Дата составления.</param>
        /// <param name="draftedByEmployeeId">Архивариус, составивший проект.</param>
        /// <param name="caseIds">Идентификаторы дел, попадающих в акт.</param>
        /// <param name="notes">Заметки (мотивированное обоснование).</param>
        DestructionAct DraftAct(
            string actNumber,
            DateTime actDate,
            int draftedByEmployeeId,
            IEnumerable<int> caseIds,
            string notes = null);

        /// <summary>
        /// Переводит акт из <see cref="DestructionStatus.Draft"/> в
        /// <see cref="DestructionStatus.Approved"/>. Состав строк фиксируется,
        /// дальнейшее редактирование запрещено.
        /// </summary>
        DestructionAct ApproveAct(int actId, int approvedByEmployeeId, DateTime approvedAt);

        /// <summary>
        /// Переводит утверждённый акт в <see cref="DestructionStatus.Executed"/>,
        /// проставляя дату фактического уничтожения и (опционально) способ.
        /// </summary>
        DestructionAct ExecuteAct(int actId, DateTime executedAt, string destructionMethod = null);

        /// <summary>
        /// Отменяет акт. Допустимо только из <see cref="DestructionStatus.Draft"/>
        /// или <see cref="DestructionStatus.Approved"/>.
        /// </summary>
        DestructionAct CancelAct(int actId, string reason = null);
    }
}
