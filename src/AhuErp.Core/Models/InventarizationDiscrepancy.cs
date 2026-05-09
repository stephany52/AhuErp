using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Строка расхождения, выявленного в ходе инвентаризации:
    /// фактический остаток против учётного с указанием причины.
    /// </summary>
    public class InventarizationDiscrepancy
    {
        public int Id { get; set; }

        public int InventarizationId { get; set; }

        public virtual Inventarization Inventarization { get; set; }

        [Required]
        [StringLength(256)]
        public string ItemName { get; set; }

        public decimal ExpectedQuantity { get; set; }

        public decimal ActualQuantity { get; set; }

        /// <summary>
        /// Расхождение — положительное излишек, отрицательное недостача.
        /// </summary>
        public decimal Delta => ActualQuantity - ExpectedQuantity;

        [StringLength(512)]
        public string Reason { get; set; }
    }
}
