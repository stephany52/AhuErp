using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IArchiveRetentionService"/>: определяет дела,
    /// у которых истёк срок хранения, формирует и сопровождает акты
    /// о выделении к уничтожению (Improvement #16 / Phase 19).
    /// </summary>
    public sealed class ArchiveRetentionService : IArchiveRetentionService
    {
        private readonly INomenclatureRepository _nomenclature;
        private readonly IDocumentRepository _documents;
        private readonly IDestructionActRepository _acts;
        private readonly IAuditLogRepository _audit;

        public ArchiveRetentionService(
            INomenclatureRepository nomenclature,
            IDocumentRepository documents,
            IDestructionActRepository acts,
            IAuditLogRepository audit)
        {
            _nomenclature = nomenclature ?? throw new ArgumentNullException(nameof(nomenclature));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _acts = acts ?? throw new ArgumentNullException(nameof(acts));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public IReadOnlyList<NomenclatureCase> FindEligibleForDestruction(DateTime asOf)
        {
            // activeOnly: false — дела, у которых истёк срок, могли быть
            // помечены IsActive=false при закрытии. Они тоже подлежат уничтожению.
            var all = _nomenclature.ListCases(year: null, activeOnly: false);
            var thresholdYear = asOf.Year;

            var eligible = all
                .Where(c => c != null
                    && c.RetentionPeriodYears > 0
                    && c.Year > 0
                    && c.Year + c.RetentionPeriodYears <= thresholdYear)
                .OrderBy(c => c.Year)
                .ThenBy(c => c.Index, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.RetentionScanCompleted,
                Timestamp = DateTime.Now,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Найдено {0} дел с истёкшим сроком хранения на {1:yyyy-MM-dd}.",
                    eligible.Count,
                    asOf)
            });

            return eligible;
        }

        public DestructionAct DraftAct(
            string actNumber,
            DateTime actDate,
            int draftedByEmployeeId,
            IEnumerable<int> caseIds,
            string notes = null)
        {
            if (string.IsNullOrWhiteSpace(actNumber))
                throw new ArgumentException("Номер акта обязателен.", nameof(actNumber));
            if (caseIds == null)
                throw new ArgumentNullException(nameof(caseIds));

            var caseIdList = caseIds.Distinct().ToList();
            if (caseIdList.Count == 0)
                throw new ArgumentException(
                    "Акт должен содержать хотя бы одно дело.", nameof(caseIds));

            var act = new DestructionAct
            {
                ActNumber = actNumber.Trim(),
                ActDate = actDate,
                Status = DestructionStatus.Draft,
                DraftedByEmployeeId = draftedByEmployeeId,
                Notes = notes
            };

            foreach (var caseId in caseIdList)
            {
                var nc = _nomenclature.GetCase(caseId)
                    ?? throw new InvalidOperationException(
                        $"Номенклатурное дело #{caseId} не найдено.");

                if (nc.RetentionPeriodYears <= 0)
                    throw new InvalidOperationException(
                        $"Дело «{nc.Index}» имеет постоянный срок хранения и не может быть включено в акт.");

                var documentCount = _documents
                    .Search(new DocumentSearchFilter { NomenclatureCaseId = caseId })
                    .Count;

                act.Items.Add(new DestructionActItem
                {
                    NomenclatureCaseId = nc.Id,
                    CaseIndex = nc.Index,
                    CaseTitle = nc.Title,
                    CaseYear = nc.Year,
                    RetentionYears = nc.RetentionPeriodYears,
                    DocumentCount = documentCount,
                    Article = nc.Article
                });
            }

            var saved = _acts.Add(act);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.DestructionActDrafted,
                Timestamp = DateTime.Now,
                UserId = draftedByEmployeeId,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Проект акта «{0}» от {1:yyyy-MM-dd}: {2} дел.",
                    saved.ActNumber,
                    saved.ActDate,
                    saved.Items.Count)
            });

            return saved;
        }

        public DestructionAct ApproveAct(int actId, int approvedByEmployeeId, DateTime approvedAt)
        {
            var act = LoadOrThrow(actId);

            if (act.Status != DestructionStatus.Draft)
                throw new InvalidOperationException(
                    $"Утвердить можно только проект акта. Текущий статус: {act.Status}.");

            act.Status = DestructionStatus.Approved;
            act.ApprovedByEmployeeId = approvedByEmployeeId;
            act.ApprovedAt = approvedAt;
            _acts.Update(act);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.DestructionActApproved,
                Timestamp = DateTime.Now,
                UserId = approvedByEmployeeId,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Акт «{0}» утверждён {1:yyyy-MM-dd}.",
                    act.ActNumber,
                    approvedAt)
            });

            return act;
        }

        public DestructionAct ExecuteAct(int actId, DateTime executedAt, string destructionMethod = null)
        {
            var act = LoadOrThrow(actId);

            if (act.Status != DestructionStatus.Approved)
                throw new InvalidOperationException(
                    $"Исполнить можно только утверждённый акт. Текущий статус: {act.Status}.");

            act.Status = DestructionStatus.Executed;
            act.ExecutedAt = executedAt;
            if (!string.IsNullOrWhiteSpace(destructionMethod))
                act.DestructionMethod = destructionMethod.Trim();
            _acts.Update(act);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.DestructionActExecuted,
                Timestamp = DateTime.Now,
                UserId = act.ApprovedByEmployeeId,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Акт «{0}» исполнен {1:yyyy-MM-dd}; способ: {2}.",
                    act.ActNumber,
                    executedAt,
                    act.DestructionMethod ?? "не указан")
            });

            return act;
        }

        public DestructionAct CancelAct(int actId, string reason = null)
        {
            var act = LoadOrThrow(actId);

            if (act.Status == DestructionStatus.Executed || act.Status == DestructionStatus.Cancelled)
                throw new InvalidOperationException(
                    $"Акт в статусе {act.Status} не может быть отменён.");

            act.Status = DestructionStatus.Cancelled;
            if (!string.IsNullOrWhiteSpace(reason))
                act.Notes = string.IsNullOrWhiteSpace(act.Notes)
                    ? reason.Trim()
                    : act.Notes + Environment.NewLine + "Отменён: " + reason.Trim();
            _acts.Update(act);

            _audit.Add(new AuditLog
            {
                ActionType = AuditActionType.DestructionActCancelled,
                Timestamp = DateTime.Now,
                Details = string.Format(
                    CultureInfo.InvariantCulture,
                    "Акт «{0}» отменён. Причина: {1}",
                    act.ActNumber,
                    string.IsNullOrWhiteSpace(reason) ? "не указана" : reason)
            });

            return act;
        }

        private DestructionAct LoadOrThrow(int actId)
        {
            var act = _acts.Get(actId);
            if (act == null)
                throw new InvalidOperationException($"Акт уничтожения #{actId} не найден.");
            return act;
        }
    }
}
