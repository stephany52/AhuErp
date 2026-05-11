using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий процедур определения поставщика (Phase 20 / Improvement #13).
    /// </summary>
    public interface IProcurementProcedureRepository
    {
        ProcurementProcedure Add(ProcurementProcedure procedure);
        ProcurementProcedure Update(ProcurementProcedure procedure);
        ProcurementProcedure Get(int id);
        IReadOnlyList<ProcurementProcedure> ListByItem(int planItemId);
        IReadOnlyList<ProcurementProcedure> List();
    }
}
