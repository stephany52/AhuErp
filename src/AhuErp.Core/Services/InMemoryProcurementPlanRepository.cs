using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация <see cref="IProcurementPlanRepository"/> для тестов.</summary>
    public sealed class InMemoryProcurementPlanRepository : IProcurementPlanRepository
    {
        private readonly Dictionary<int, ProcurementPlan> _plans = new Dictionary<int, ProcurementPlan>();
        private readonly Dictionary<int, ProcurementPlanItem> _items = new Dictionary<int, ProcurementPlanItem>();
        private int _nextPlanId = 1;
        private int _nextItemId = 1;

        public ProcurementPlan Add(ProcurementPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.Year <= 0)
                throw new ArgumentException("Год плана обязателен.", nameof(plan));
            if (string.IsNullOrWhiteSpace(plan.Title))
                throw new ArgumentException("Наименование плана обязательно.", nameof(plan));
            if (_plans.Values.Any(p => p.Year == plan.Year))
                throw new InvalidOperationException(
                    $"План закупок на {plan.Year} год уже зарегистрирован.");

            plan.Id = _nextPlanId++;
            _plans[plan.Id] = plan;
            return plan;
        }

        public ProcurementPlan Update(ProcurementPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!_plans.ContainsKey(plan.Id))
                throw new InvalidOperationException("План закупок не найден.");
            _plans[plan.Id] = plan;
            return plan;
        }

        public ProcurementPlan Get(int id) => _plans.TryGetValue(id, out var p) ? p : null;

        public ProcurementPlan GetByYear(int year)
            => _plans.Values.FirstOrDefault(p => p.Year == year);

        public IReadOnlyList<ProcurementPlan> List()
            => _plans.Values.OrderByDescending(p => p.Year).ToList().AsReadOnly();

        public ProcurementPlanItem AddItem(ProcurementPlanItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_plans.ContainsKey(item.ProcurementPlanId))
                throw new InvalidOperationException(
                    $"План закупок #{item.ProcurementPlanId} не найден.");
            if (string.IsNullOrWhiteSpace(item.Okpd2Code))
                throw new ArgumentException("Код ОКПД2 обязателен.", nameof(item));
            if (string.IsNullOrWhiteSpace(item.Subject))
                throw new ArgumentException("Наименование объекта закупки обязательно.", nameof(item));
            if (item.InitialMaxPrice <= 0)
                throw new ArgumentException("НМЦК должна быть положительной.", nameof(item));

            item.Id = _nextItemId++;
            _items[item.Id] = item;
            return item;
        }

        public ProcurementPlanItem UpdateItem(ProcurementPlanItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_items.ContainsKey(item.Id))
                throw new InvalidOperationException("Позиция плана не найдена.");
            _items[item.Id] = item;
            return item;
        }

        public ProcurementPlanItem GetItem(int itemId)
            => _items.TryGetValue(itemId, out var i) ? i : null;

        public IReadOnlyList<ProcurementPlanItem> ListItems(int planId)
            => _items.Values.Where(i => i.ProcurementPlanId == planId)
                .OrderBy(i => i.LineNumber).ToList().AsReadOnly();
    }
}
