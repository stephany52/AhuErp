namespace AhuErp.Core.Models
{
    /// <summary>
    /// Вид инструктажа по охране труда / пожарной безопасности.
    /// Соответствует п. 2.1 ГОСТ 12.0.004-2015 и Приказу МЧС № 645.
    /// </summary>
    public enum BriefingKind
    {
        /// <summary>Вводный — при поступлении на работу.</summary>
        Initial = 0,

        /// <summary>Первичный на рабочем месте.</summary>
        PrimaryAtWorkplace = 1,

        /// <summary>Повторный — не реже одного раза в полугодие.</summary>
        Recurring = 2,

        /// <summary>Целевой — перед выполнением разовых работ.</summary>
        Targeted = 3,

        /// <summary>Внеплановый — при изменении нормативов / после аварий.</summary>
        Unscheduled = 4
    }
}
