using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IInventoryRepository"/>. Использует длинноживущий
    /// <see cref="AhuDbContext"/>, поэтому изменения <see cref="InventoryItem.TotalQuantity"/>
    /// в <see cref="InventoryService.ProcessTransaction"/> автоматически отслеживаются
    /// EF6 и сохраняются в одной транзакции с записью <see cref="InventoryTransaction"/>.
    /// </summary>
    public sealed class EfInventoryRepository : IInventoryRepository
    {
        private readonly AhuDbContext _ctx;

        public EfInventoryRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public IReadOnlyList<InventoryItem> ListItems() =>
            _ctx.InventoryItems.ToList().AsReadOnly();

        public InventoryItem GetItem(int itemId) => _ctx.InventoryItems.Find(itemId);

        public IReadOnlyList<InventoryTransaction> ListTransactions(int? itemId = null)
        {
            // Bug #5. Жадно подгружаем именованные навигации, иначе UI
            // (DataGrid «Последние движения») рисует Id вместо названия
            // позиции и регистрационного номера документа-основания.
            IQueryable<InventoryTransaction> q = _ctx.InventoryTransactions
                .Include(t => t.InventoryItem)
                .Include(t => t.Document)
                .Include(t => t.Initiator);
            if (itemId.HasValue)
            {
                q = q.Where(t => t.InventoryItemId == itemId.Value);
            }
            return q.OrderByDescending(t => t.TransactionDate)
                    .ToList()
                    .AsReadOnly();
        }

        public void AddItem(InventoryItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _ctx.InventoryItems.Add(item);
            _ctx.SaveChanges();
        }

        public void RecordTransaction(InventoryTransaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            _ctx.InventoryTransactions.Add(transaction);
            // SaveChanges одной транзакцией флашит и Add, и mutated TotalQuantity
            // на отслеживаемом item (см. InventoryService.ProcessTransaction).
            _ctx.SaveChanges();
        }
    }
}
