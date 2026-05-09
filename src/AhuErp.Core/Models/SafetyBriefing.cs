using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Запись журнала инструктажей по охране труда / пожарной безопасности.
    /// Improvement #12 / Phase 15. Поле <see cref="SignatureConfirmed"/> фиксирует
    /// подтверждение факта инструктажа подписью инструктируемого; реквизиты
    /// подписи в бумажной/электронной форме хранятся отдельно (см. Phase 8).
    /// </summary>
    public class SafetyBriefing
    {
        public int Id { get; set; }

        public DateTime BriefingDate { get; set; }

        public BriefingKind Kind { get; set; }

        /// <summary>Тема инструктажа (например, «Правила работы за ПК»).</summary>
        [Required]
        [StringLength(256)]
        public string Topic { get; set; }

        /// <summary>Сотрудник, прошедший инструктаж.</summary>
        public int TraineeEmployeeId { get; set; }

        public virtual Employee TraineeEmployee { get; set; }

        /// <summary>Сотрудник, проводящий инструктаж.</summary>
        public int InstructorEmployeeId { get; set; }

        public virtual Employee InstructorEmployee { get; set; }

        /// <summary>
        /// Признак подписи инструктируемого. В бумажном журнале — собственноручная
        /// подпись; в системе — флаг подтверждения, аудит фиксирует автора.
        /// </summary>
        public bool SignatureConfirmed { get; set; }

        [StringLength(2048)]
        public string Notes { get; set; }
    }
}
