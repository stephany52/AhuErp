using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory репозиторий маршрутов согласования (для тестов).</summary>
    public sealed class InMemoryApprovalRepository : IApprovalRepository
    {
        private readonly List<ApprovalRouteTemplate> _templates = new List<ApprovalRouteTemplate>();
        private readonly List<ApprovalStage> _stages = new List<ApprovalStage>();
        private readonly List<DocumentApproval> _approvals = new List<DocumentApproval>();
        private int _nextTemplateId = 1, _nextStageId = 1, _nextApprovalId = 1;

        public IReadOnlyList<ApprovalRouteTemplate> ListTemplates(bool activeOnly)
        {
            IEnumerable<ApprovalRouteTemplate> q = _templates;
            if (activeOnly) q = q.Where(t => t.IsActive);
            return q.OrderBy(t => t.Name).ToList().AsReadOnly();
        }

        public ApprovalRouteTemplate GetTemplate(int id) => _templates.FirstOrDefault(t => t.Id == id);

        public ApprovalRouteTemplate AddTemplate(ApprovalRouteTemplate template)
        {
            if (template.Id == 0) template.Id = _nextTemplateId++;
            _templates.Add(template);
            return template;
        }

        public ApprovalStage AddStage(ApprovalStage stage)
        {
            if (stage.Id == 0) stage.Id = _nextStageId++;
            _stages.Add(stage);
            return stage;
        }

        public IReadOnlyList<ApprovalStage> ListStages(int templateId)
            => _stages.Where(s => s.RouteTemplateId == templateId)
                      .OrderBy(s => s.Order)
                      .ToList()
                      .AsReadOnly();

        public DocumentApproval AddApproval(DocumentApproval approval)
        {
            if (approval.Id == 0) approval.Id = _nextApprovalId++;
            _approvals.Add(approval);
            return approval;
        }

        public DocumentApproval GetApproval(int id) => _approvals.FirstOrDefault(a => a.Id == id);

        public void UpdateApproval(DocumentApproval approval)
        {
            var idx = _approvals.FindIndex(a => a.Id == approval.Id);
            if (idx >= 0) _approvals[idx] = approval;
        }

        public IReadOnlyList<DocumentApproval> ListApprovalsByDocument(int documentId)
            => _approvals.Where(a => a.DocumentId == documentId)
                         .OrderBy(a => a.Order)
                         .ToList()
                         .AsReadOnly();

        public IReadOnlyList<DocumentApproval> ListApprovalsByApprover(int approverId, ApprovalDecision? decision = null)
        {
            IEnumerable<DocumentApproval> q = _approvals.Where(a => a.ApproverId == approverId);
            if (decision.HasValue) q = q.Where(a => a.Decision == decision.Value);
            return q.OrderByDescending(a => a.DecisionDate ?? System.DateTime.MinValue)
                    .ThenBy(a => a.Order)
                    .ToList()
                    .AsReadOnly();
        }
    }
}
