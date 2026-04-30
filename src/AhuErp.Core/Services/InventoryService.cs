using System;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IInventoryService"/> поверх <see cref="IInventoryRepository"/>.
    /// Сервис остаётся инфраструктурно-независимым: репозиторий сам решает,
    /// как сохранять данные (EF6 или in-memory). Бизнес-правила:
    /// <list type="bullet">
    ///   <item><description><c>quantityChange == 0</c> → <see cref="ArgumentException"/>.</description></item>
    ///   <item><description>Списание: <c>TotalQuantity + quantityChange &gt;= 0</c>. Иначе — <see cref="InvalidOperationException"/>.</description></item>
    ///   <item><description>Списание обязано иметь <paramref name="documentId"/> (документ-основание).</description></item>
    /// </list>
    /// </summary>
    public sealed class InventoryService : IInventoryService
    {
        private readonly IInventoryRepository _repository;

        public InventoryService(IInventoryRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public InventoryTransaction ProcessTransaction(int itemId, int quantityChange, int? documentId, int userId)
        {
            if (quantityChange == 0)
            {
                throw new ArgumentException(
                    "Количество изменения не может быть нулевым.", nameof(quantityChange));
            }
            if (userId <= 0)
            {
                throw new ArgumentException(
                    "Инициатор операции обязателен.", nameof(userId));
            }

            var item = _repository.GetItem(itemId)
                ?? throw new InvalidOperationException($"Номенклатурная позиция #{itemId} не найдена.");

            if (quantityChange < 0)
            {
                if (!documentId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Списание возможно только на основании документа.");
                }
                if (item.TotalQuantity + quantityChange < 0)
                {
                    throw new InvalidOperationException(
                        $"Недостаточно остатка: требуется {-quantityChange}, доступно {item.TotalQuantity}.");
                }
            }

            item.TotalQuantity += quantityChange;
            if (string.IsNullOrWhiteSpace(item.Unit)) item.Unit = "шт.";

            var transaction = new InventoryTransaction
            {
                InventoryItemId = item.Id,
                DocumentId = documentId,
                BasisDocumentId = documentId,
                QuantityChanged = quantityChange,
                TransactionDate = DateTime.Now,
                InitiatorId = userId
            };

            _repository.RecordTransaction(transaction);
            return transaction;
        }
    }
}
