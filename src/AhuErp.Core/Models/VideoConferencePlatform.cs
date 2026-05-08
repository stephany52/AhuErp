namespace AhuErp.Core.Models
{
    /// <summary>
    /// Платформа видеоконференцсвязи. В МКУ «АХУ» БМР используется
    /// преимущественно ВКС-площадка администрации района и дополнительные
    /// внешние сервисы для совещаний с подрядчиками.
    /// </summary>
    /// <remarks>
    /// Целочисленные значения зафиксированы — менять нельзя, чтобы не
    /// сломать сохранённые значения в БД.
    /// </remarks>
    public enum VideoConferencePlatform
    {
        /// <summary>Региональная ВКС-площадка (Минцифры / администрация).</summary>
        RegionalVks = 0,

        /// <summary>Jitsi Meet (self-hosted / web).</summary>
        Jitsi = 1,

        /// <summary>Zoom.</summary>
        Zoom = 2,

        /// <summary>Microsoft Teams.</summary>
        MsTeams = 3,

        /// <summary>Google Meet.</summary>
        GoogleMeet = 4,

        /// <summary>Прочая платформа.</summary>
        Other = 99
    }
}
