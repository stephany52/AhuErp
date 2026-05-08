namespace AhuErp.Core.Models
{
    /// <summary>
    /// Категория оборудования ИТО. Классификация совпадает с реестром
    /// должностной инструкции системного администратора МКУ «АХУ» БМР
    /// (info.txt: ПК, оргтехника, сетевое оборудование, IP-телефония, ВКС,
    /// видеонаблюдение).
    /// </summary>
    /// <remarks>
    /// Целочисленные значения зафиксированы — менять нельзя, чтобы не сломать
    /// сохранённые значения в БД.
    /// </remarks>
    public enum EquipmentType
    {
        /// <summary>Персональный компьютер / ноутбук пользователя.</summary>
        Pc = 0,

        /// <summary>Принтер / МФУ.</summary>
        Printer = 1,

        /// <summary>Коммутатор / маршрутизатор.</summary>
        Switch = 2,

        /// <summary>Точка доступа Wi-Fi.</summary>
        AccessPoint = 3,

        /// <summary>IP-телефон.</summary>
        IpPhone = 4,

        /// <summary>IP-камера видеонаблюдения.</summary>
        IpCamera = 5,

        /// <summary>Сервер / сетевое хранилище.</summary>
        Server = 6,

        /// <summary>Оборудование ВКС (камера, микрофон, codec).</summary>
        VideoConferenceUnit = 7,

        /// <summary>Источник бесперебойного питания.</summary>
        Ups = 8,

        /// <summary>Прочее оборудование.</summary>
        Other = 99
    }
}
