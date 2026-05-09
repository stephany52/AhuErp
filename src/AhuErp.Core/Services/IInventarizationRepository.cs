using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Хранилище журнала инвентаризаций. Improvement #12 / Phase 15.
    /// </summary>
    public interface IInventarizationRepository
    {
        IReadOnlyList<Inventarization> List(DateTime? from, DateTime? to, InventarizationScope? scope);
        Inventarization GetById(int id);
        void Add(Inventarization inventarization);
        void Update(Inventarization inventarization);
        void Remove(int id);
    }
}
