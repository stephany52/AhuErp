using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий планов-графиков закупок (Phase 20 / Improvement #13).
    /// </summary>
    public interface IProcurementPlanRepository
    {
        ProcurementPlan Add(ProcurementPlan plan);
        ProcurementPlan Update(ProcurementPlan plan);
        ProcurementPlan Get(int id);
        ProcurementPlan GetByYear(int year);
        IReadOnlyList<ProcurementPlan> List();

        ProcurementPlanItem AddItem(ProcurementPlanItem item);
        ProcurementPlanItem UpdateItem(ProcurementPlanItem item);
        ProcurementPlanItem GetItem(int itemId);
        IReadOnlyList<ProcurementPlanItem> ListItems(int planId);
    }
}
