using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Строка акта о выделении к уничтожению — снимок данных номенклатурного
    /// дела на момент составления акта (Improvement #16 / Phase 19). Хранится
    /// денормализованно, чтобы акт оставался читаемым даже после удаления
    /// исходного <see cref="NomenclatureCase"/>.
    /// </summary>
    public class DestructionActItem
    {
        public int Id { get; set; }

        /// <summary>FK на родительский акт. Каскадное удаление вместе с актом.</summary>
        public int DestructionActId { get; set; }
        public virtual DestructionAct DestructionAct { get; set; }

        /// <summary>
        /// FK на номенклатурное дело (nullable — дело может быть удалено
        /// после исполнения акта; реквизиты сохраняются в строке).
        /// </summary>
        public int? NomenclatureCaseId { get; set; }
        public virtual NomenclatureCase NomenclatureCase { get; set; }

        /// <summary>Индекс дела на момент составления акта (например, «01-07»).</summary>
        [Required]
        [StringLength(32)]
        public string CaseIndex { get; set; }

        /// <summary>Заголовок дела на момент составления акта.</summary>
        [Required]
        [StringLength(512)]
        public string CaseTitle { get; set; }

        /// <summary>Год дела (год начала ведения).</summary>
        public int CaseYear { get; set; }

        /// <summary>Срок хранения дела (лет; 0 — постоянное, в акт попасть не может).</summary>
        public int RetentionYears { get; set; }

        /// <summary>Количество единиц хранения (документов) в деле на момент акта.</summary>
        public int DocumentCount { get; set; }

        /// <summary>
        /// Статья типового перечня документов со сроками хранения (например,
        /// «ст. 19, Перечень 2019 г.»). Снимок <see cref="NomenclatureCase.Article"/>.
        /// </summary>
        [StringLength(64)]
        public string Article { get; set; }

        /// <summary>Заметки по строке (например, «частично уничтожено: л. 1–5»).</summary>
        [StringLength(1024)]
        public string Notes { get; set; }
    }
}
