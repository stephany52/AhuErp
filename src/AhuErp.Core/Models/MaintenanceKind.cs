namespace AhuErp.Core.Models
{
    /// <summary>
    /// Профиль эксплуатационной заявки. Improvement #15 / Phase 18.
    /// Используется для маршрутизации заявки нужному исполнителю
    /// (электрик / сантехник / хозяйственник).
    /// </summary>
    public enum MaintenanceKind
    {
        /// <summary>Прочее / неклассифицировано.</summary>
        Other = 0,

        /// <summary>Электрика (свет, розетки, щит).</summary>
        Electrical = 1,

        /// <summary>Сантехника (водоснабжение, канализация).</summary>
        Plumbing = 2,

        /// <summary>Отопление, вентиляция, кондиционирование (HVAC).</summary>
        Hvac = 3,

        /// <summary>Плотницкие / столярные работы (двери, окна, мебель).</summary>
        Carpentry = 4,

        /// <summary>Уборка / клининг.</summary>
        Cleaning = 5,

        /// <summary>Косметический / капитальный ремонт.</summary>
        Repair = 6,

        /// <summary>Слаботочка / СКС / охранная сигнализация.</summary>
        LowCurrent = 7,
    }
}
