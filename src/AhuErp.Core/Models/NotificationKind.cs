namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 9 — типы in-app/e-mail уведомлений рабочего стола сотрудника.
    /// Соответствуют ключевым событиям документооборота.
    /// </summary>
    public enum NotificationKind
    {
        TaskAssigned = 0,
        TaskDeadlineSoon = 1,
        TaskOverdue = 2,
        ApprovalRequired = 3,
        ApprovalDecided = 4,
        ResolutionAdded = 5,
        DocumentRegistered = 6,
        DocumentSigned = 7,

        // ---- Phase 17 / Improvement #14 — автопарк: ОСАГО / ТО / ТО по пробегу. ----
        VehicleOsagoExpiringSoon = 10,
        VehicleTechInspectionExpiringSoon = 11,
        VehicleMaintenanceDueSoon = 12,

        System = 99,
    }
}
