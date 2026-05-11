using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IDestructionActRepository"/> для тестов
    /// (Improvement #16 / Phase 19). Идентификаторы выдаются автоинкрементом,
    /// уникальность <see cref="DestructionAct.ActNumber"/> проверяется на уровне
    /// репозитория, чтобы поведение совпадало с уникальным индексом в EF6.
    /// </summary>
    public sealed class InMemoryDestructionActRepository : IDestructionActRepository
    {
        private readonly Dictionary<int, DestructionAct> _acts = new Dictionary<int, DestructionAct>();
        private readonly Dictionary<int, DestructionActItem> _items = new Dictionary<int, DestructionActItem>();
        private int _nextActId = 1;
        private int _nextItemId = 1;

        public DestructionAct Add(DestructionAct act)
        {
            if (act == null) throw new ArgumentNullException(nameof(act));
            if (string.IsNullOrWhiteSpace(act.ActNumber))
                throw new ArgumentException("Номер акта обязателен.", nameof(act));
            if (_acts.Values.Any(a => a.ActNumber == act.ActNumber))
                throw new InvalidOperationException(
                    $"Акт с номером «{act.ActNumber}» уже зарегистрирован.");

            act.Id = _nextActId++;
            _acts[act.Id] = act;

            if (act.Items != null)
            {
                foreach (var item in act.Items.ToList())
                {
                    item.DestructionActId = act.Id;
                    AddItem(item);
                }
            }
            return act;
        }

        public DestructionAct Get(int id)
        {
            if (!_acts.TryGetValue(id, out var act)) return null;
            HydrateItems(act);
            return act;
        }

        public DestructionAct GetByActNumber(string actNumber)
        {
            if (string.IsNullOrWhiteSpace(actNumber)) return null;
            var act = _acts.Values.FirstOrDefault(a => a.ActNumber == actNumber);
            if (act != null) HydrateItems(act);
            return act;
        }

        public IReadOnlyList<DestructionAct> List()
        {
            return _acts.Values
                .OrderByDescending(a => a.ActDate)
                .ThenByDescending(a => a.Id)
                .Select(a => { HydrateItems(a); return a; })
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<DestructionAct> ListByStatus(DestructionStatus status)
        {
            return _acts.Values
                .Where(a => a.Status == status)
                .OrderByDescending(a => a.ActDate)
                .ThenByDescending(a => a.Id)
                .Select(a => { HydrateItems(a); return a; })
                .ToList()
                .AsReadOnly();
        }

        public DestructionAct Update(DestructionAct act)
        {
            if (act == null) throw new ArgumentNullException(nameof(act));
            if (!_acts.ContainsKey(act.Id))
                throw new InvalidOperationException("Акт не найден.");
            _acts[act.Id] = act;
            return act;
        }

        public DestructionActItem AddItem(DestructionActItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_acts.ContainsKey(item.DestructionActId))
                throw new InvalidOperationException("Родительский акт не найден.");

            item.Id = _nextItemId++;
            _items[item.Id] = item;
            return item;
        }

        public void RemoveItem(int itemId)
        {
            _items.Remove(itemId);
        }

        private void HydrateItems(DestructionAct act)
        {
            var items = _items.Values
                .Where(i => i.DestructionActId == act.Id)
                .OrderBy(i => i.Id)
                .ToList();
            act.Items = new HashSet<DestructionActItem>(items);
        }
    }
}
