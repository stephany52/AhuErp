namespace AhuErp.Core.Models
{
    /// <summary>
    /// Функциональное назначение помещения. Improvement #15 / Phase 18. Используется
    /// для фильтрации в журнале помещений и для привязки заявок на эксплуатационные
    /// работы (например, серверные требуют отдельного контура согласований).
    /// </summary>
    public enum RoomPurpose
    {
        /// <summary>Не указано / иное.</summary>
        Other = 0,

        /// <summary>Кабинет / рабочее помещение.</summary>
        Office = 1,

        /// <summary>Серверная.</summary>
        ServerRoom = 2,

        /// <summary>Складское помещение.</summary>
        Storage = 3,

        /// <summary>Архивохранилище.</summary>
        Archive = 4,

        /// <summary>Гараж / стоянка.</summary>
        Garage = 5,

        /// <summary>Зал заседаний / конференц-зал.</summary>
        MeetingRoom = 6,

        /// <summary>Санузел / коммуникации.</summary>
        Sanitary = 7,

        /// <summary>Технический этаж / щитовая.</summary>
        Technical = 8,
    }
}
