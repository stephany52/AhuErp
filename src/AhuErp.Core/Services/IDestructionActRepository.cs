using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Репозиторий актов о выделении к уничтожению (Improvement #16 / Phase 19).
    /// Все операции загружают строки акта (<see cref="DestructionActItem"/>)
    /// вместе с заголовком, чтобы DOCX-печать и журналы не делали лишних
    /// круговых запросов.
    /// </summary>
    public interface IDestructionActRepository
    {
        DestructionAct Add(DestructionAct act);

        DestructionAct Get(int id);

        DestructionAct GetByActNumber(string actNumber);

        IReadOnlyList<DestructionAct> List();

        IReadOnlyList<DestructionAct> ListByStatus(DestructionStatus status);

        DestructionAct Update(DestructionAct act);

        DestructionActItem AddItem(DestructionActItem item);

        void RemoveItem(int itemId);
    }
}
