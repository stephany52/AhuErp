namespace AhuErp.Core.Models
{
    /// <summary>
    /// Категория действия в журнале аудита. Список расширяемый — добавление
    /// новых членов в конец не требует миграции (значение хранится как int).
    /// </summary>
    public enum AuditActionType
    {
        Created = 0,
        Updated = 1,
        Deleted = 2,

        StatusChanged = 10,
        Registered = 11,
        AssignedToCase = 12,

        AttachmentAdded = 20,
        AttachmentVersioned = 21,
        AttachmentRemoved = 22,
        AttachmentViewed = 23,

        ResolutionIssued = 30,
        TaskAssigned = 31,
        TaskCompleted = 32,
        TaskOverdue = 33,
        TaskReassigned = 34,

        ApprovalSent = 40,
        ApprovalSigned = 41,
        ApprovalRejected = 42,

        InventoryTransactionRecorded = 50,
        VehicleTripBooked = 51,
        ArchiveRequestProcessed = 52,
        ItTicketResolved = 53,

        // Phase 8 — электронные подписи и блокировка документа.
        SignatureAdded = 60,
        SignatureRevoked = 61,
        DocumentLocked = 62,
        DocumentUnlocked = 63,

        // Phase 9 — нотификации.
        NotificationSent = 70,

        // Phase 11 — оргструктура и замещения.
        SubstitutionCreated = 80,
        SubstitutionCancelled = 81,
        TaskDelegated = 82,
        DepartmentHeadAssigned = 83,

        // Phase 10 / 12 — поиск и регламентированная отчётность.
        IndexRebuilt = 85,
        ReportGenerated = 86,

        UserLogin = 90,
        UserLogout = 91,

        // Phase 16 / Improvement #17 — расширенный аудит безопасности.
        LoginAttemptFailed = 92,
        AccountLocked = 93,
        PasswordChanged = 94,
        PasswordExpired = 95,
        AccountUnlocked = 96,
        ConfidentialDocumentViewed = 97,

        DocumentExportedToExcel = 100,
        DocumentExportedToPdf = 101,
        DocumentPrinted = 102,
        DocumentDownloaded = 103,
        RoleChanged = 104,
        SubstitutionChanged = 105,
        EncryptionKeyRotated = 106,

        // Phase 17 / Improvement #14 — экспорт DOCX-форм (путевой лист и
        // другие формальные документы), отдельно от Excel/PDF, чтобы
        // фильтры в админ-панели не объединяли разные форматы.
        DocumentExportedToDocx = 107,

        Other = 99
    }
}
