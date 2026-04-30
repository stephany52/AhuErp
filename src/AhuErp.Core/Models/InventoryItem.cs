using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Номенклатурная позиция склада (ТМЦ). <see cref="TotalQuantity"/> отражает
    /// актуальный остаток и пересчитывается через <see cref="InventoryTransaction"/>.
    /// </summary>
    public class InventoryItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(256)]
        public string Name { get; set; }

        public InventoryCategory Category { get; set; }

        [StringLength(32)]
        public string Unit { get; set; }

        public int MinimumBalance { get; set; }

        /// <summary>Текущий остаток (в штуках).</summary>
        public int TotalQuantity { get; set; }

        public virtual ICollection<InventoryTransaction> Transactions { get; set; }
            = new List<InventoryTransaction>();
    }
}
