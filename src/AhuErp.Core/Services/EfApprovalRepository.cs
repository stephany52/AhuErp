using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-репозиторий маршрутов согласования.</summary>
    public sealed class EfApprovalRepository : IApprovalRepository
    {
        private readonly AhuDbContext _ctx;

        public EfApprovalRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public IReadOnlyList<ApprovalRouteTemplate> ListTemplates(bool activeOnly)
        {
            IQueryable<ApprovalRouteTemplate> q = _ctx.ApprovalRouteTemplates;
            if (activeOnly) q = q.Where(t => t.IsActive);
            return q.OrderBy(t => t.Name).ToList().AsReadOnly();
        }

        public ApprovalRouteTemplate GetTemplate(int id) => _ctx.ApprovalRouteTemplates.Find(id);

        public ApprovalRouteTemplate AddTemplate(ApprovalRouteTemplate template)
        {
            _ctx.ApprovalRouteTemplates.Add(template);
            _ctx.SaveChanges();
            return template;
        }

        public ApprovalStage AddStage(ApprovalStage stage)
        {
            _ctx.ApprovalStages.Add(stage);
            _ctx.SaveChanges();
            return stage;
        }

        public IReadOnlyList<ApprovalStage> ListStages(int templateId)
            => _ctx.ApprovalStages.Where(s => s.RouteTemplateId == templateId)
                .OrderBy(s => s.Order).ToList().AsReadOnly();

        public DocumentApproval AddApproval(DocumentApproval approval)
        {
            _ctx.DocumentApprovals.Add(approval);
            _ctx.SaveChanges();
            return approval;
        }

        public DocumentApproval GetApproval(int id) => _ctx.DocumentApprovals.Find(id);

        public void UpdateApproval(DocumentApproval approval)
        {
            if (_ctx.Entry(approval).State == EntityState.Detached)
            {
                _ctx.DocumentApprovals.Attach(approval);
                _ctx.Entry(approval).State = EntityState.Modified;
            }
            _ctx.SaveChanges();
        }

        public IReadOnlyList<DocumentApproval> ListApprovalsByDocument(int documentId)
            => _ctx.DocumentApprovals.Include(a => a.Approver)
                .Where(a => a.DocumentId == documentId)
                .OrderBy(a => a.Order).ToList().AsReadOnly();

        public IReadOnlyList<DocumentApproval> ListApprovalsByApprover(int approverId, ApprovalDecision? decision = null)
        {
            IQueryable<DocumentApproval> q = _ctx.DocumentApprovals
                .Include(a => a.Document)
                .Include(a => a.Approver)
                .Where(a => a.ApproverId == approverId);
            if (decision.HasValue)
            {
                var d = decision.Value;
                q = q.Where(a => a.Decision == d);
            }
            return q.OrderByDescending(a => a.DecisionDate)
                    .ThenBy(a => a.Order)
                    .ToList()
                    .AsReadOnly();
        }
    }
}
