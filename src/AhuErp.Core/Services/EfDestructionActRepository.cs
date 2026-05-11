using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IDestructionActRepository"/>
    /// (Improvement #16 / Phase 19).
    /// </summary>
    public sealed class EfDestructionActRepository : IDestructionActRepository
    {
        private readonly AhuDbContext _ctx;

        public EfDestructionActRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public DestructionAct Add(DestructionAct act)
        {
            if (act == null) throw new ArgumentNullException(nameof(act));
            if (string.IsNullOrWhiteSpace(act.ActNumber))
                throw new ArgumentException("Номер акта обязателен.", nameof(act));

            if (_ctx.DestructionActs.Any(a => a.ActNumber == act.ActNumber))
                throw new InvalidOperationException(
                    $"Акт с номером «{act.ActNumber}» уже зарегистрирован.");

            _ctx.DestructionActs.Add(act);
            _ctx.SaveChanges();
            return act;
        }

        public DestructionAct Get(int id)
        {
            return _ctx.DestructionActs
                .Include(a => a.Items)
                .FirstOrDefault(a => a.Id == id);
        }

        public DestructionAct GetByActNumber(string actNumber)
        {
            if (string.IsNullOrWhiteSpace(actNumber)) return null;
            return _ctx.DestructionActs
                .Include(a => a.Items)
                .FirstOrDefault(a => a.ActNumber == actNumber);
        }

        public IReadOnlyList<DestructionAct> List()
            => _ctx.DestructionActs
                .Include(a => a.Items)
                .OrderByDescending(a => a.ActDate)
                .ThenByDescending(a => a.Id)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<DestructionAct> ListByStatus(DestructionStatus status)
            => _ctx.DestructionActs
                .Include(a => a.Items)
                .Where(a => a.Status == status)
                .OrderByDescending(a => a.ActDate)
                .ThenByDescending(a => a.Id)
                .ToList()
                .AsReadOnly();

        public DestructionAct Update(DestructionAct act)
        {
            if (act == null) throw new ArgumentNullException(nameof(act));
            _ctx.Entry(act).State = EntityState.Modified;
            _ctx.SaveChanges();
            return act;
        }

        public DestructionActItem AddItem(DestructionActItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_ctx.DestructionActs.Any(a => a.Id == item.DestructionActId))
                throw new InvalidOperationException("Родительский акт не найден.");

            _ctx.DestructionActItems.Add(item);
            _ctx.SaveChanges();
            return item;
        }

        public void RemoveItem(int itemId)
        {
            var existing = _ctx.DestructionActItems.Find(itemId);
            if (existing == null) return;
            _ctx.DestructionActItems.Remove(existing);
            _ctx.SaveChanges();
        }
    }
}
