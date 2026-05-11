using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="IProcurementPlanRepository"/>.</summary>
    public sealed class EfProcurementPlanRepository : IProcurementPlanRepository
    {
        private readonly AhuDbContext _ctx;

        public EfProcurementPlanRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public ProcurementPlan Add(ProcurementPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.Year <= 0)
                throw new ArgumentException("Год плана обязателен.", nameof(plan));
            if (string.IsNullOrWhiteSpace(plan.Title))
                throw new ArgumentException("Наименование плана обязательно.", nameof(plan));
            if (_ctx.ProcurementPlans.Any(p => p.Year == plan.Year))
                throw new InvalidOperationException(
                    $"План закупок на {plan.Year} год уже зарегистрирован.");

            _ctx.ProcurementPlans.Add(plan);
            _ctx.SaveChanges();
            return plan;
        }

        public ProcurementPlan Update(ProcurementPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            _ctx.Entry(plan).State = EntityState.Modified;
            _ctx.SaveChanges();
            return plan;
        }

        public ProcurementPlan Get(int id) => _ctx.ProcurementPlans.Find(id);

        public ProcurementPlan GetByYear(int year)
            => _ctx.ProcurementPlans.FirstOrDefault(p => p.Year == year);

        public IReadOnlyList<ProcurementPlan> List()
            => _ctx.ProcurementPlans.OrderByDescending(p => p.Year).ToList().AsReadOnly();

        public ProcurementPlanItem AddItem(ProcurementPlanItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (_ctx.ProcurementPlans.Find(item.ProcurementPlanId) == null)
                throw new InvalidOperationException(
                    $"План закупок #{item.ProcurementPlanId} не найден.");
            if (string.IsNullOrWhiteSpace(item.Okpd2Code))
                throw new ArgumentException("Код ОКПД2 обязателен.", nameof(item));
            if (string.IsNullOrWhiteSpace(item.Subject))
                throw new ArgumentException("Наименование объекта закупки обязательно.", nameof(item));
            if (item.InitialMaxPrice <= 0)
                throw new ArgumentException("НМЦК должна быть положительной.", nameof(item));

            _ctx.ProcurementPlanItems.Add(item);
            _ctx.SaveChanges();
            return item;
        }

        public ProcurementPlanItem UpdateItem(ProcurementPlanItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _ctx.Entry(item).State = EntityState.Modified;
            _ctx.SaveChanges();
            return item;
        }

        public ProcurementPlanItem GetItem(int itemId) => _ctx.ProcurementPlanItems.Find(itemId);

        public IReadOnlyList<ProcurementPlanItem> ListItems(int planId)
            => _ctx.ProcurementPlanItems
                .Where(i => i.ProcurementPlanId == planId)
                .OrderBy(i => i.LineNumber)
                .ToList()
                .AsReadOnly();
    }
}
