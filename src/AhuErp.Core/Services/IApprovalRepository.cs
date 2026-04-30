using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>Доступ к данным маршрутов согласования.</summary>
    public interface IApprovalRepository
    {
        IReadOnlyList<ApprovalRouteTemplate> ListTemplates(bool activeOnly);
        ApprovalRouteTemplate GetTemplate(int id);
        ApprovalRouteTemplate AddTemplate(ApprovalRouteTemplate template);
        ApprovalStage AddStage(ApprovalStage stage);
        IReadOnlyList<ApprovalStage> ListStages(int templateId);

        DocumentApproval AddApproval(DocumentApproval approval);
        DocumentApproval GetApproval(int id);
        void UpdateApproval(DocumentApproval approval);
        IReadOnlyList<DocumentApproval> ListApprovalsByDocument(int documentId);
        IReadOnlyList<DocumentApproval> ListApprovalsByApprover(int approverId, ApprovalDecision? decision = null);
    }
}
