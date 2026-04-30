using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Управление маршрутами согласования документов.
    /// </summary>
    public interface IApprovalService
    {
        IReadOnlyList<ApprovalRouteTemplate> ListTemplates(bool activeOnly = true);
        ApprovalRouteTemplate GetTemplate(int id);
        ApprovalRouteTemplate AddTemplate(ApprovalRouteTemplate template);
        ApprovalStage AddStage(int templateId, ApprovalStage stage);

        /// <summary>
        /// Запустить согласование документа по шаблону маршрута.
        /// Создаёт <see cref="DocumentApproval"/> для каждого этапа шаблона,
        /// устанавливает <see cref="ApprovalRouteStatus.InProgress"/>.
        /// </summary>
        IReadOnlyList<DocumentApproval> StartApproval(int documentId, int templateId, int actorId);

        DocumentApproval ApplyDecision(int approvalId, ApprovalDecision decision, int actorId, string comment = null);

        IReadOnlyList<DocumentApproval> ListByDocument(int documentId);
        IReadOnlyList<DocumentApproval> ListForApprover(int approverId, ApprovalDecision? decision = null);
    }
}
