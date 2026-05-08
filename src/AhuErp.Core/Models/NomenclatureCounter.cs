using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AhuErp.Core.Models
{
    public class NomenclatureCounter
    {
        public int Id { get; set; }

        [Required]
        [StringLength(16)]
        [Index("UX_NomenclatureCounter_TypeCode_Year", 1, IsUnique = true)]
        public string TypeCode { get; set; }

        [Index("UX_NomenclatureCounter_TypeCode_Year", 2, IsUnique = true)]
        public int Year { get; set; }

        public int LastNumber { get; set; }
    }
}
