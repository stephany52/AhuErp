/* ============================================================================
 * AhuErp — создание схемы БД (SQL Server).
 *
 * Собрано по EF6 Code-First миграциям:
 *   1) 20260423121238_InitialCreate
 *   2) 20260423125626_AddEmployeeAuth
 *   3) 20260423131841_AddInventoryAndItTicket
 *   4) 20260423175847_AddVehicleTripDriverName
 *   5) 20260426113552_AddArchiveRequestKind
 *   6) 20260426140000_AddEnterpriseEDMSFeatures (Phase 7)
 *
 * Запуск в SQL Server Management Studio:
 *   1. Подключиться к DESKTOP-I1OTVEB\SQLEXPRESS (Windows Auth).
 *   2. File → Open → create-db.sql → F5.
 *   3. Проверить: SELECT name FROM sys.databases WHERE name = 'AhuErpDb';
 *
 * Скрипт идемпотентен: повторный запуск не упадёт на существующих объектах.
 * Идентификаторы (IDENTITY) EF6 создаёт как INT, автогенерация с 1.
 * ========================================================================== */

USE [master];
GO

IF DB_ID(N'AhuErpDb') IS NULL
BEGIN
    CREATE DATABASE [AhuErpDb];
END
GO

USE [AhuErpDb];
GO

/* ---------- 1. Employees ---------------------------------------------------- */
IF OBJECT_ID(N'dbo.Employees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees
    (
        Id            INT            IDENTITY(1, 1) NOT NULL,
        FullName      NVARCHAR(256)  NOT NULL,
        [Position]    NVARCHAR(256)  NULL,
        [Role]        INT            NOT NULL CONSTRAINT DF_Employees_Role DEFAULT (0),
        PasswordHash  NVARCHAR(512)  NULL,
        CONSTRAINT PK_dbo_Employees PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* ---------- 1a. Departments (Phase 7) -------------------------------------- */
IF OBJECT_ID(N'dbo.Departments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Departments
    (
        Id         INT            IDENTITY(1, 1) NOT NULL,
        Name       NVARCHAR(256)  NOT NULL,
        ShortCode  NVARCHAR(16)   NULL,
        IsActive   BIT            NOT NULL CONSTRAINT DF_Departments_IsActive DEFAULT (1),
        CONSTRAINT PK_dbo_Departments PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* ---------- 1b. DocumentTypeRefs (Phase 7) --------------------------------- */
IF OBJECT_ID(N'dbo.DocumentTypeRefs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentTypeRefs
    (
        Id                          INT            IDENTITY(1, 1) NOT NULL,
        Name                        NVARCHAR(256)  NOT NULL,
        ShortCode                   NVARCHAR(16)   NULL,
        DefaultDirection            INT            NOT NULL CONSTRAINT DF_DocumentTypeRefs_Dir DEFAULT (0),
        DefaultRetentionYears       INT            NOT NULL CONSTRAINT DF_DocumentTypeRefs_Ret DEFAULT (5),
        RegistrationNumberTemplate  NVARCHAR(128)  NULL,
        IsActive                    BIT            NOT NULL CONSTRAINT DF_DocumentTypeRefs_IsActive DEFAULT (1),
        CONSTRAINT PK_dbo_DocumentTypeRefs PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* ---------- 1c. NomenclatureCases (Phase 7) -------------------------------- */
IF OBJECT_ID(N'dbo.NomenclatureCases', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NomenclatureCases
    (
        Id                    INT            IDENTITY(1, 1) NOT NULL,
        [Index]               NVARCHAR(32)   NOT NULL,
        Title                 NVARCHAR(512)  NOT NULL,
        DepartmentId          INT            NULL,
        RetentionPeriodYears  INT            NOT NULL,
        Article               NVARCHAR(64)   NULL,
        [Year]                INT            NOT NULL,
        IsActive              BIT            NOT NULL CONSTRAINT DF_NomenclatureCases_IsActive DEFAULT (1),
        CONSTRAINT PK_dbo_NomenclatureCases PRIMARY KEY CLUSTERED (Id ASC)
    );

    ALTER TABLE dbo.NomenclatureCases
        ADD CONSTRAINT [FK_dbo.NomenclatureCases_dbo.Departments_DepartmentId]
        FOREIGN KEY (DepartmentId)
        REFERENCES dbo.Departments (Id);

    CREATE NONCLUSTERED INDEX IX_NomenclatureCases_DepartmentId
        ON dbo.NomenclatureCases (DepartmentId);
END
GO

/* ---------- 2. Documents (TPH: Document / ArchiveRequest / ItTicket) -------- */
IF OBJECT_ID(N'dbo.Documents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Documents
    (
        Id                     INT             IDENTITY(1, 1) NOT NULL,
        [Type]                 INT             NOT NULL,
        Direction              INT             NOT NULL CONSTRAINT DF_Documents_Direction DEFAULT (0),
        AccessLevel            INT             NOT NULL CONSTRAINT DF_Documents_AccessLevel DEFAULT (0),
        RegistrationNumber     NVARCHAR(64)    NULL,
        RegistrationDate       DATETIME        NULL,
        DocumentTypeRefId      INT             NULL,
        NomenclatureCaseId     INT             NULL,
        AuthorId               INT             NULL,
        Title                  NVARCHAR(512)   NOT NULL,
        Summary                NVARCHAR(4000)  NULL,
        Correspondent          NVARCHAR(512)   NULL,
        IncomingNumber         NVARCHAR(64)    NULL,
        IncomingDate           DATETIME        NULL,
        CreationDate           DATETIME        NOT NULL,
        Deadline               DATETIME        NOT NULL,
        [Status]               INT             NOT NULL,
        AssignedEmployeeId     INT             NULL,
        BasisDocumentId        INT             NULL,
        ApprovalStatus         INT             NOT NULL CONSTRAINT DF_Documents_ApprovalStatus DEFAULT (0),
        HasPassportScan        BIT             NULL,
        HasWorkBookScan        BIT             NULL,
        ArchiveRequestKind     INT             NULL,
        AffectedEquipment      NVARCHAR(256)   NULL,
        ResolutionNotes        NVARCHAR(1024)  NULL,
        /* Phase 14: расширение модуля ИТО — каталог оборудования + поставщик + MTTR */
        AffectedEquipmentId    INT             NULL,
        Kind                   INT             NOT NULL CONSTRAINT DF_Documents_Kind DEFAULT (0),
        IsSentToVendor         BIT             NOT NULL CONSTRAINT DF_Documents_IsSentToVendor DEFAULT (0),
        VendorName             NVARCHAR(256)   NULL,
        VendorTicketNumber     NVARCHAR(64)    NULL,
        VendorReturnDeadline   DATETIME        NULL,
        CompletedAt            DATETIME        NULL,
        DocumentDiscriminator  NVARCHAR(128)   NOT NULL,
        CONSTRAINT PK_dbo_Documents PRIMARY KEY CLUSTERED (Id ASC)
    );

    ALTER TABLE dbo.Documents
        ADD CONSTRAINT [FK_dbo.Documents_dbo.Employees_AssignedEmployeeId]
        FOREIGN KEY (AssignedEmployeeId)
        REFERENCES dbo.Employees (Id);

    ALTER TABLE dbo.Documents
        ADD CONSTRAINT [FK_dbo.Documents_dbo.Employees_AuthorId]
        FOREIGN KEY (AuthorId)
        REFERENCES dbo.Employees (Id);

    ALTER TABLE dbo.Documents
        ADD CONSTRAINT [FK_dbo.Documents_dbo.DocumentTypeRefs_DocumentTypeRefId]
        FOREIGN KEY (DocumentTypeRefId)
        REFERENCES dbo.DocumentTypeRefs (Id);

    ALTER TABLE dbo.Documents
        ADD CONSTRAINT [FK_dbo.Documents_dbo.NomenclatureCases_NomenclatureCaseId]
        FOREIGN KEY (NomenclatureCaseId)
        REFERENCES dbo.NomenclatureCases (Id);

    ALTER TABLE dbo.Documents
        ADD CONSTRAINT [FK_dbo.Documents_dbo.Documents_BasisDocumentId]
        FOREIGN KEY (BasisDocumentId)
        REFERENCES dbo.Documents (Id);

    CREATE NONCLUSTERED INDEX IX_Documents_AssignedEmployeeId ON dbo.Documents (AssignedEmployeeId);
    CREATE NONCLUSTERED INDEX IX_Documents_AuthorId           ON dbo.Documents (AuthorId);
    CREATE NONCLUSTERED INDEX IX_Documents_DocumentTypeRefId  ON dbo.Documents (DocumentTypeRefId);
    CREATE NONCLUSTERED INDEX IX_Documents_NomenclatureCaseId ON dbo.Documents (NomenclatureCaseId);
    CREATE NONCLUSTERED INDEX IX_Documents_BasisDocumentId    ON dbo.Documents (BasisDocumentId);
END
GO

/* ---------- 3. Vehicles ---------------------------------------------------- */
IF OBJECT_ID(N'dbo.Vehicles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Vehicles
    (
        Id             INT            IDENTITY(1, 1) NOT NULL,
        Model          NVARCHAR(128)  NOT NULL,
        LicensePlate   NVARCHAR(32)   NOT NULL,
        CurrentStatus  INT            NOT NULL,
        CONSTRAINT PK_dbo_Vehicles PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* ---------- 3a. Vehicles: учёт ГСМ (Phase 15 / Improvement #12) ----------- */
IF COL_LENGTH(N'dbo.Vehicles', N'FuelType') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles
        ADD FuelType                 INT             NOT NULL CONSTRAINT DF_Vehicles_FuelType DEFAULT (0),
            FuelConsumptionPer100Km  DECIMAL(18, 2)  NOT NULL CONSTRAINT DF_Vehicles_FuelConsumptionPer100Km DEFAULT (0);
END
GO

/* ---------- 4. VehicleTrips ------------------------------------------------ */
IF OBJECT_ID(N'dbo.VehicleTrips', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleTrips
    (
        Id          INT            IDENTITY(1, 1) NOT NULL,
        VehicleId   INT            NOT NULL,
        StartDate   DATETIME       NOT NULL,
        EndDate     DATETIME       NOT NULL,
        DocumentId  INT            NULL,
        DriverName  NVARCHAR(128)  NULL,
        CONSTRAINT PK_dbo_VehicleTrips PRIMARY KEY CLUSTERED (Id ASC)
    );

    ALTER TABLE dbo.VehicleTrips
        ADD CONSTRAINT [FK_dbo.VehicleTrips_dbo.Vehicles_VehicleId]
        FOREIGN KEY (VehicleId)
        REFERENCES dbo.Vehicles (Id)
        ON DELETE CASCADE; /* CascadeOnDelete(true) */

    ALTER TABLE dbo.VehicleTrips
        ADD CONSTRAINT [FK_dbo.VehicleTrips_dbo.Documents_DocumentId]
        FOREIGN KEY (DocumentId)
        REFERENCES dbo.Documents (Id); /* WillCascadeOnDelete(false) */

    CREATE NONCLUSTERED INDEX IX_VehicleTrips_VehicleId
        ON dbo.VehicleTrips (VehicleId);

    CREATE NONCLUSTERED INDEX IX_VehicleTrips_DocumentId
        ON dbo.VehicleTrips (DocumentId);
END
GO

/* ---------- 4a. VehicleTrips: BasisDocumentId (Phase 7) -------------------- */
IF COL_LENGTH(N'dbo.VehicleTrips', N'BasisDocumentId') IS NULL
BEGIN
    ALTER TABLE dbo.VehicleTrips ADD BasisDocumentId INT NULL;

    ALTER TABLE dbo.VehicleTrips
        ADD CONSTRAINT [FK_dbo.VehicleTrips_dbo.Documents_BasisDocumentId]
        FOREIGN KEY (BasisDocumentId)
        REFERENCES dbo.Documents (Id);

    CREATE NONCLUSTERED INDEX IX_VehicleTrips_BasisDocumentId
        ON dbo.VehicleTrips (BasisDocumentId);
END
GO

/* ---------- 4b. VehicleTrips: учёт ГСМ (Phase 15 / Improvement #12) -------- */
IF COL_LENGTH(N'dbo.VehicleTrips', N'OdometerStart') IS NULL
BEGIN
    ALTER TABLE dbo.VehicleTrips
        ADD OdometerStart    INT             NULL,
            OdometerEnd      INT             NULL,
            FuelIssuedLiters DECIMAL(9, 2)   NULL,
            Route            NVARCHAR(512)   NULL,
            PassengerNames   NVARCHAR(1024)  NULL,
            ActualStart      DATETIME        NULL,
            ActualEnd        DATETIME        NULL;
END
GO

/* ---------- 5. InventoryItems --------------------------------------------- */
IF OBJECT_ID(N'dbo.InventoryItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryItems
    (
        Id             INT            IDENTITY(1, 1) NOT NULL,
        [Name]         NVARCHAR(256)  NOT NULL,
        Category       INT            NOT NULL,
        TotalQuantity  INT            NOT NULL,
        CONSTRAINT PK_dbo_InventoryItems PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* ---------- 6. InventoryTransactions -------------------------------------- */
IF OBJECT_ID(N'dbo.InventoryTransactions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryTransactions
    (
        Id               INT       IDENTITY(1, 1) NOT NULL,
        InventoryItemId  INT       NOT NULL,
        DocumentId       INT       NULL,
        QuantityChanged  INT       NOT NULL,
        TransactionDate  DATETIME  NOT NULL,
        InitiatorId      INT       NOT NULL,
        CONSTRAINT PK_dbo_InventoryTransactions PRIMARY KEY CLUSTERED (Id ASC)
    );

    ALTER TABLE dbo.InventoryTransactions
        ADD CONSTRAINT [FK_dbo.InventoryTransactions_dbo.InventoryItems_InventoryItemId]
        FOREIGN KEY (InventoryItemId)
        REFERENCES dbo.InventoryItems (Id)
        ON DELETE CASCADE; /* CascadeOnDelete(true) */

    ALTER TABLE dbo.InventoryTransactions
        ADD CONSTRAINT [FK_dbo.InventoryTransactions_dbo.Documents_DocumentId]
        FOREIGN KEY (DocumentId)
        REFERENCES dbo.Documents (Id); /* WillCascadeOnDelete(false) */

    ALTER TABLE dbo.InventoryTransactions
        ADD CONSTRAINT [FK_dbo.InventoryTransactions_dbo.Employees_InitiatorId]
        FOREIGN KEY (InitiatorId)
        REFERENCES dbo.Employees (Id); /* WillCascadeOnDelete(false) */

    CREATE NONCLUSTERED INDEX IX_InventoryTransactions_InventoryItemId
        ON dbo.InventoryTransactions (InventoryItemId);

    CREATE NONCLUSTERED INDEX IX_InventoryTransactions_DocumentId
        ON dbo.InventoryTransactions (DocumentId);

    CREATE NONCLUSTERED INDEX IX_InventoryTransactions_InitiatorId
        ON dbo.InventoryTransactions (InitiatorId);
END
GO

/* ---------- 6a. InventoryTransactions: BasisDocumentId (Phase 7) ----------- */
IF COL_LENGTH(N'dbo.InventoryTransactions', N'BasisDocumentId') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryTransactions ADD BasisDocumentId INT NULL;

    ALTER TABLE dbo.InventoryTransactions
        ADD CONSTRAINT [FK_dbo.InventoryTransactions_dbo.Documents_BasisDocumentId]
        FOREIGN KEY (BasisDocumentId)
        REFERENCES dbo.Documents (Id);

    CREATE NONCLUSTERED INDEX IX_InventoryTransactions_BasisDocumentId
        ON dbo.InventoryTransactions (BasisDocumentId);
END
GO

/* ---------- 7. DocumentCaseLinks (Phase 7) --------------------------------- */
IF OBJECT_ID(N'dbo.DocumentCaseLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentCaseLinks
    (
        Id                  INT       IDENTITY(1, 1) NOT NULL,
        DocumentId          INT       NOT NULL,
        NomenclatureCaseId  INT       NOT NULL,
        LinkedAt            DATETIME  NOT NULL,
        LinkedById          INT       NULL,
        IsPrimary           BIT       NOT NULL CONSTRAINT DF_DocumentCaseLinks_IsPrimary DEFAULT (0),
        CONSTRAINT PK_dbo_DocumentCaseLinks PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.DocumentCaseLinks_dbo.Documents_DocumentId]
            FOREIGN KEY (DocumentId) REFERENCES dbo.Documents (Id) ON DELETE CASCADE,
        CONSTRAINT [FK_dbo.DocumentCaseLinks_dbo.NomenclatureCases_NomenclatureCaseId]
            FOREIGN KEY (NomenclatureCaseId) REFERENCES dbo.NomenclatureCases (Id),
        CONSTRAINT [FK_dbo.DocumentCaseLinks_dbo.Employees_LinkedById]
            FOREIGN KEY (LinkedById) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_DocumentCaseLinks_DocumentId         ON dbo.DocumentCaseLinks (DocumentId);
    CREATE NONCLUSTERED INDEX IX_DocumentCaseLinks_NomenclatureCaseId ON dbo.DocumentCaseLinks (NomenclatureCaseId);
    CREATE NONCLUSTERED INDEX IX_DocumentCaseLinks_LinkedById         ON dbo.DocumentCaseLinks (LinkedById);
END
GO

/* ---------- 8. DocumentAttachments (Phase 7) ------------------------------- */
IF OBJECT_ID(N'dbo.DocumentAttachments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentAttachments
    (
        Id                  INT             IDENTITY(1, 1) NOT NULL,
        DocumentId          INT             NOT NULL,
        AttachmentGroupId   INT             NOT NULL,
        FileName            NVARCHAR(512)   NOT NULL,
        StoragePath         NVARCHAR(1024)  NOT NULL,
        VersionNumber       INT             NOT NULL,
        IsCurrentVersion    BIT             NOT NULL,
        UploadedAt          DATETIME        NOT NULL,
        UploadedById        INT             NOT NULL,
        Comment             NVARCHAR(1024)  NULL,
        Hash                NVARCHAR(128)   NULL,
        FileType            INT             NOT NULL,
        SizeBytes           BIGINT          NOT NULL,
        CONSTRAINT PK_dbo_DocumentAttachments PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.DocumentAttachments_dbo.Documents_DocumentId]
            FOREIGN KEY (DocumentId) REFERENCES dbo.Documents (Id) ON DELETE CASCADE,
        CONSTRAINT [FK_dbo.DocumentAttachments_dbo.Employees_UploadedById]
            FOREIGN KEY (UploadedById) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_DocumentAttachments_DocumentId        ON dbo.DocumentAttachments (DocumentId);
    CREATE NONCLUSTERED INDEX IX_DocumentAttachments_UploadedById      ON dbo.DocumentAttachments (UploadedById);
    CREATE NONCLUSTERED INDEX IX_DocumentAttachments_AttachmentGroupId ON dbo.DocumentAttachments (AttachmentGroupId);
END
GO

/* ---------- 9. DocumentResolutions (Phase 7) ------------------------------- */
IF OBJECT_ID(N'dbo.DocumentResolutions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentResolutions
    (
        Id          INT             IDENTITY(1, 1) NOT NULL,
        DocumentId  INT             NOT NULL,
        AuthorId    INT             NOT NULL,
        Text        NVARCHAR(2048)  NOT NULL,
        IssuedAt    DATETIME        NOT NULL,
        CONSTRAINT PK_dbo_DocumentResolutions PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.DocumentResolutions_dbo.Documents_DocumentId]
            FOREIGN KEY (DocumentId) REFERENCES dbo.Documents (Id) ON DELETE CASCADE,
        CONSTRAINT [FK_dbo.DocumentResolutions_dbo.Employees_AuthorId]
            FOREIGN KEY (AuthorId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_DocumentResolutions_DocumentId ON dbo.DocumentResolutions (DocumentId);
    CREATE NONCLUSTERED INDEX IX_DocumentResolutions_AuthorId   ON dbo.DocumentResolutions (AuthorId);
END
GO

/* ---------- 10. DocumentTasks (Phase 7) ------------------------------------ */
IF OBJECT_ID(N'dbo.DocumentTasks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentTasks
    (
        Id            INT             IDENTITY(1, 1) NOT NULL,
        DocumentId    INT             NOT NULL,
        ResolutionId  INT             NULL,
        ParentTaskId  INT             NULL,
        AuthorId      INT             NOT NULL,
        ExecutorId    INT             NOT NULL,
        ControllerId  INT             NULL,
        CoExecutors   NVARCHAR(1024)  NULL,
        Description   NVARCHAR(2048)  NOT NULL,
        CreatedAt     DATETIME        NOT NULL,
        Deadline      DATETIME        NOT NULL,
        [Status]      INT             NOT NULL,
        CompletedAt   DATETIME        NULL,
        ReportText    NVARCHAR(2048)  NULL,
        IsCritical    BIT             NOT NULL CONSTRAINT DF_DocumentTasks_IsCritical DEFAULT (0),
        CONSTRAINT PK_dbo_DocumentTasks PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.DocumentTasks_dbo.Documents_DocumentId]
            FOREIGN KEY (DocumentId) REFERENCES dbo.Documents (Id),
        CONSTRAINT [FK_dbo.DocumentTasks_dbo.DocumentResolutions_ResolutionId]
            FOREIGN KEY (ResolutionId) REFERENCES dbo.DocumentResolutions (Id),
        CONSTRAINT [FK_dbo.DocumentTasks_dbo.DocumentTasks_ParentTaskId]
            FOREIGN KEY (ParentTaskId) REFERENCES dbo.DocumentTasks (Id),
        CONSTRAINT [FK_dbo.DocumentTasks_dbo.Employees_AuthorId]
            FOREIGN KEY (AuthorId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.DocumentTasks_dbo.Employees_ExecutorId]
            FOREIGN KEY (ExecutorId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.DocumentTasks_dbo.Employees_ControllerId]
            FOREIGN KEY (ControllerId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_DocumentTasks_DocumentId   ON dbo.DocumentTasks (DocumentId);
    CREATE NONCLUSTERED INDEX IX_DocumentTasks_ResolutionId ON dbo.DocumentTasks (ResolutionId);
    CREATE NONCLUSTERED INDEX IX_DocumentTasks_ParentTaskId ON dbo.DocumentTasks (ParentTaskId);
    CREATE NONCLUSTERED INDEX IX_DocumentTasks_AuthorId     ON dbo.DocumentTasks (AuthorId);
    CREATE NONCLUSTERED INDEX IX_DocumentTasks_ExecutorId   ON dbo.DocumentTasks (ExecutorId);
    CREATE NONCLUSTERED INDEX IX_DocumentTasks_ControllerId ON dbo.DocumentTasks (ControllerId);
END
GO

/* ---------- 11. ApprovalRouteTemplates (Phase 7) --------------------------- */
IF OBJECT_ID(N'dbo.ApprovalRouteTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApprovalRouteTemplates
    (
        Id                 INT             IDENTITY(1, 1) NOT NULL,
        Name               NVARCHAR(256)   NOT NULL,
        Description        NVARCHAR(1024)  NULL,
        IsActive           BIT             NOT NULL CONSTRAINT DF_ApprovalRouteTemplates_IsActive DEFAULT (1),
        DocumentTypeRefId  INT             NULL,
        CONSTRAINT PK_dbo_ApprovalRouteTemplates PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.ApprovalRouteTemplates_dbo.DocumentTypeRefs_DocumentTypeRefId]
            FOREIGN KEY (DocumentTypeRefId) REFERENCES dbo.DocumentTypeRefs (Id)
    );
    CREATE NONCLUSTERED INDEX IX_ApprovalRouteTemplates_DocumentTypeRefId ON dbo.ApprovalRouteTemplates (DocumentTypeRefId);
END
GO

/* ---------- 12. ApprovalStages (Phase 7) ----------------------------------- */
IF OBJECT_ID(N'dbo.ApprovalStages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApprovalStages
    (
        Id                  INT            IDENTITY(1, 1) NOT NULL,
        RouteTemplateId     INT            NOT NULL,
        [Order]             INT            NOT NULL,
        IsParallel          BIT            NOT NULL CONSTRAINT DF_ApprovalStages_IsParallel DEFAULT (0),
        ApproverEmployeeId  INT            NULL,
        ApproverRole        INT            NULL,
        Description         NVARCHAR(512)  NULL,
        CONSTRAINT PK_dbo_ApprovalStages PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.ApprovalStages_dbo.ApprovalRouteTemplates_RouteTemplateId]
            FOREIGN KEY (RouteTemplateId) REFERENCES dbo.ApprovalRouteTemplates (Id) ON DELETE CASCADE,
        CONSTRAINT [FK_dbo.ApprovalStages_dbo.Employees_ApproverEmployeeId]
            FOREIGN KEY (ApproverEmployeeId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_ApprovalStages_RouteTemplateId    ON dbo.ApprovalStages (RouteTemplateId);
    CREATE NONCLUSTERED INDEX IX_ApprovalStages_ApproverEmployeeId ON dbo.ApprovalStages (ApproverEmployeeId);
END
GO

/* ---------- 13. DocumentApprovals (Phase 7) -------------------------------- */
IF OBJECT_ID(N'dbo.DocumentApprovals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentApprovals
    (
        Id            INT             IDENTITY(1, 1) NOT NULL,
        DocumentId    INT             NOT NULL,
        StageId       INT             NULL,
        [Order]       INT             NOT NULL,
        IsParallel    BIT             NOT NULL CONSTRAINT DF_DocumentApprovals_IsParallel DEFAULT (0),
        ApproverId    INT             NOT NULL,
        Decision      INT             NOT NULL CONSTRAINT DF_DocumentApprovals_Decision DEFAULT (0),
        Comment       NVARCHAR(2048)  NULL,
        DecisionDate  DATETIME        NULL,
        CONSTRAINT PK_dbo_DocumentApprovals PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.DocumentApprovals_dbo.Documents_DocumentId]
            FOREIGN KEY (DocumentId) REFERENCES dbo.Documents (Id) ON DELETE CASCADE,
        CONSTRAINT [FK_dbo.DocumentApprovals_dbo.ApprovalStages_StageId]
            FOREIGN KEY (StageId) REFERENCES dbo.ApprovalStages (Id),
        CONSTRAINT [FK_dbo.DocumentApprovals_dbo.Employees_ApproverId]
            FOREIGN KEY (ApproverId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_DocumentApprovals_DocumentId ON dbo.DocumentApprovals (DocumentId);
    CREATE NONCLUSTERED INDEX IX_DocumentApprovals_StageId    ON dbo.DocumentApprovals (StageId);
    CREATE NONCLUSTERED INDEX IX_DocumentApprovals_ApproverId ON dbo.DocumentApprovals (ApproverId);
END
GO

/* ---------- 13a. SafetyBriefings (Phase 15 / Improvement #12) ------------- */
IF OBJECT_ID(N'dbo.SafetyBriefings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SafetyBriefings
    (
        Id                    INT             IDENTITY(1, 1) NOT NULL,
        BriefingDate          DATETIME        NOT NULL,
        Kind                  INT             NOT NULL,
        Topic                 NVARCHAR(256)   NOT NULL,
        TraineeEmployeeId     INT             NOT NULL,
        InstructorEmployeeId  INT             NOT NULL,
        SignatureConfirmed    BIT             NOT NULL,
        Notes                 NVARCHAR(2048)  NULL,
        CONSTRAINT PK_dbo_SafetyBriefings PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.SafetyBriefings_dbo.Employees_TraineeEmployeeId]
            FOREIGN KEY (TraineeEmployeeId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.SafetyBriefings_dbo.Employees_InstructorEmployeeId]
            FOREIGN KEY (InstructorEmployeeId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_SafetyBriefings_TraineeEmployeeId
        ON dbo.SafetyBriefings (TraineeEmployeeId);
    CREATE NONCLUSTERED INDEX IX_SafetyBriefings_InstructorEmployeeId
        ON dbo.SafetyBriefings (InstructorEmployeeId);
    CREATE NONCLUSTERED INDEX IX_SafetyBriefings_Date_Kind
        ON dbo.SafetyBriefings (BriefingDate, Kind);
END
GO

/* ---------- 13b. Inventarizations (Phase 15 / Improvement #12) ------------ */
IF OBJECT_ID(N'dbo.Inventarizations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Inventarizations
    (
        Id                  INT             IDENTITY(1, 1) NOT NULL,
        StartDate           DATETIME        NOT NULL,
        EndDate             DATETIME        NULL,
        Scope               INT             NOT NULL,
        ScopeDescription    NVARCHAR(256)   NOT NULL,
        CommissionMembers   NVARCHAR(2048)  NULL,
        ChairmanId          INT             NULL,
        ResultDocumentId    INT             NULL,
        Notes               NVARCHAR(2048)  NULL,
        CONSTRAINT PK_dbo_Inventarizations PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.Inventarizations_dbo.Employees_ChairmanId]
            FOREIGN KEY (ChairmanId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.Inventarizations_dbo.Documents_ResultDocumentId]
            FOREIGN KEY (ResultDocumentId) REFERENCES dbo.Documents (Id)
    );
    CREATE NONCLUSTERED INDEX IX_Inventarizations_ChairmanId
        ON dbo.Inventarizations (ChairmanId);
    CREATE NONCLUSTERED INDEX IX_Inventarizations_ResultDocumentId
        ON dbo.Inventarizations (ResultDocumentId);
    CREATE NONCLUSTERED INDEX IX_Inventarizations_Date_Scope
        ON dbo.Inventarizations (StartDate, Scope);
END
GO

/* ---------- 13c. InventarizationDiscrepancies (Phase 15 / Improvement #12)  */
IF OBJECT_ID(N'dbo.InventarizationDiscrepancies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventarizationDiscrepancies
    (
        Id                INT             IDENTITY(1, 1) NOT NULL,
        InventarizationId INT             NOT NULL,
        ItemName          NVARCHAR(256)   NOT NULL,
        ExpectedQuantity  DECIMAL(18, 3)  NOT NULL,
        ActualQuantity    DECIMAL(18, 3)  NOT NULL,
        Reason            NVARCHAR(512)   NULL,
        CONSTRAINT PK_dbo_InventarizationDiscrepancies PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.InventarizationDiscrepancies_dbo.Inventarizations_InventarizationId]
            FOREIGN KEY (InventarizationId) REFERENCES dbo.Inventarizations (Id) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_InventarizationDiscrepancies_InventarizationId
        ON dbo.InventarizationDiscrepancies (InventarizationId);
END
GO

/* ---------- 13d. ArchiveTransfers (Phase 15 / Improvement #12) ------------ */
IF OBJECT_ID(N'dbo.ArchiveTransfers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArchiveTransfers
    (
        Id                  INT            IDENTITY(1, 1) NOT NULL,
        NomenclatureCaseId  INT            NOT NULL,
        TransferDate        DATETIME       NOT NULL,
        TransferredById     INT            NULL,
        AcceptedById        INT            NULL,
        ActDocumentId       INT            NULL,
        ArchiveCode         NVARCHAR(64)   NULL,
        RetentionYears      INT            NOT NULL,
        Notes               NVARCHAR(2048) NULL,
        CONSTRAINT PK_dbo_ArchiveTransfers PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.ArchiveTransfers_dbo.NomenclatureCases_NomenclatureCaseId]
            FOREIGN KEY (NomenclatureCaseId) REFERENCES dbo.NomenclatureCases (Id),
        CONSTRAINT [FK_dbo.ArchiveTransfers_dbo.Employees_TransferredById]
            FOREIGN KEY (TransferredById) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.ArchiveTransfers_dbo.Employees_AcceptedById]
            FOREIGN KEY (AcceptedById) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.ArchiveTransfers_dbo.Documents_ActDocumentId]
            FOREIGN KEY (ActDocumentId) REFERENCES dbo.Documents (Id)
    );
    CREATE NONCLUSTERED INDEX IX_ArchiveTransfers_NomenclatureCaseId
        ON dbo.ArchiveTransfers (NomenclatureCaseId);
    CREATE NONCLUSTERED INDEX IX_ArchiveTransfers_TransferredById
        ON dbo.ArchiveTransfers (TransferredById);
    CREATE NONCLUSTERED INDEX IX_ArchiveTransfers_AcceptedById
        ON dbo.ArchiveTransfers (AcceptedById);
    CREATE NONCLUSTERED INDEX IX_ArchiveTransfers_ActDocumentId
        ON dbo.ArchiveTransfers (ActDocumentId);
    CREATE NONCLUSTERED INDEX IX_ArchiveTransfers_Date
        ON dbo.ArchiveTransfers (TransferDate);
END
GO

/* ---------- 14. AuditLogs (Phase 7, immutable, hash-цепочка) --------------- */
IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs
    (
        Id            INT             IDENTITY(1, 1) NOT NULL,
        Timestamp     DATETIME        NOT NULL,
        UserId        INT             NULL,
        ActionType    INT             NOT NULL,
        EntityType    NVARCHAR(128)   NULL,
        EntityId      INT             NULL,
        OldValues     NVARCHAR(4000)  NULL,
        NewValues     NVARCHAR(4000)  NULL,
        Details       NVARCHAR(1024)  NULL,
        Hash          NVARCHAR(128)   NULL,
        PreviousHash  NVARCHAR(128)   NULL,
        CONSTRAINT PK_dbo_AuditLogs PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.AuditLogs_dbo.Employees_UserId]
            FOREIGN KEY (UserId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_AuditLogs_UserId     ON dbo.AuditLogs (UserId);
    CREATE NONCLUSTERED INDEX IX_AuditLogs_Timestamp  ON dbo.AuditLogs (Timestamp);
    CREATE NONCLUSTERED INDEX IX_AuditLogs_EntityType ON dbo.AuditLogs (EntityType);
END
GO

/* ---------- 15. NetworkSegments (Phase 14) -------------------------------- */
IF OBJECT_ID(N'dbo.NetworkSegments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NetworkSegments
    (
        Id           INT             IDENTITY(1, 1) NOT NULL,
        [Name]       NVARCHAR(128)   NOT NULL,
        Vlan         NVARCHAR(32)    NULL,
        IpRange      NVARCHAR(64)    NULL,
        SubnetMask   NVARCHAR(64)    NULL,
        Gateway      NVARCHAR(64)    NULL,
        Dns          NVARCHAR(128)   NULL,
        Notes        NVARCHAR(1024)  NULL,
        CONSTRAINT PK_dbo_NetworkSegments PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* ---------- 16. Equipment (Phase 14) -------------------------------------- */
IF OBJECT_ID(N'dbo.Equipment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Equipment
    (
        Id                     INT             IDENTITY(1, 1) NOT NULL,
        InventoryNumber        NVARCHAR(64)    NOT NULL,
        [Type]                 INT             NOT NULL CONSTRAINT DF_Equipment_Type DEFAULT (0),
        Model                  NVARCHAR(256)   NULL,
        SerialNumber           NVARCHAR(128)   NULL,
        MacAddress             NVARCHAR(32)    NULL,
        IpAddress              NVARCHAR(64)    NULL,
        Room                   NVARCHAR(128)   NULL,
        ResponsibleEmployeeId  INT             NULL,
        InServiceDate          DATETIME        NULL,
        WarrantyExpiry         DATETIME        NULL,
        [Status]               INT             NOT NULL CONSTRAINT DF_Equipment_Status DEFAULT (0),
        NetworkSegmentId       INT             NULL,
        Notes                  NVARCHAR(1024)  NULL,
        CONSTRAINT PK_dbo_Equipment PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.Equipment_dbo.Employees_ResponsibleEmployeeId]
            FOREIGN KEY (ResponsibleEmployeeId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.Equipment_dbo.NetworkSegments_NetworkSegmentId]
            FOREIGN KEY (NetworkSegmentId) REFERENCES dbo.NetworkSegments (Id)
    );
    CREATE NONCLUSTERED INDEX IX_Equipment_ResponsibleEmployeeId ON dbo.Equipment (ResponsibleEmployeeId);
    CREATE NONCLUSTERED INDEX IX_Equipment_NetworkSegmentId      ON dbo.Equipment (NetworkSegmentId);

    /* Document.AffectedEquipmentId FK к Equipment (после создания таблицы Equipment) */
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_dbo.Documents_dbo.Equipment_AffectedEquipmentId')
    BEGIN
        ALTER TABLE dbo.Documents
            ADD CONSTRAINT [FK_dbo.Documents_dbo.Equipment_AffectedEquipmentId]
            FOREIGN KEY (AffectedEquipmentId)
            REFERENCES dbo.Equipment (Id);
        CREATE NONCLUSTERED INDEX IX_Documents_AffectedEquipmentId ON dbo.Documents (AffectedEquipmentId);
    END
END
GO

/* ---------- 17. ItTicketDiagnosticEntries (Phase 14) ---------------------- */
IF OBJECT_ID(N'dbo.ItTicketDiagnosticEntries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItTicketDiagnosticEntries
    (
        Id         INT             IDENTITY(1, 1) NOT NULL,
        TicketId   INT             NOT NULL,
        AuthorId   INT             NULL,
        Timestamp  DATETIME        NOT NULL,
        Action     NVARCHAR(1024)  NOT NULL,
        Category   NVARCHAR(128)   NULL,
        CONSTRAINT PK_dbo_ItTicketDiagnosticEntries PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.ItTicketDiagnosticEntries_dbo.Documents_TicketId]
            FOREIGN KEY (TicketId) REFERENCES dbo.Documents (Id) ON DELETE CASCADE,
        CONSTRAINT [FK_dbo.ItTicketDiagnosticEntries_dbo.Employees_AuthorId]
            FOREIGN KEY (AuthorId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_ItTicketDiagnosticEntries_TicketId  ON dbo.ItTicketDiagnosticEntries (TicketId);
    CREATE NONCLUSTERED INDEX IX_ItTicketDiagnosticEntries_AuthorId  ON dbo.ItTicketDiagnosticEntries (AuthorId);
    CREATE NONCLUSTERED INDEX IX_ItTicketDiagnosticEntries_Timestamp ON dbo.ItTicketDiagnosticEntries (Timestamp);
END
GO

/* ---------- 18. VideoConferences (Phase 14) ------------------------------- */
IF OBJECT_ID(N'dbo.VideoConferences', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VideoConferences
    (
        Id            INT             IDENTITY(1, 1) NOT NULL,
        Topic         NVARCHAR(256)   NOT NULL,
        ScheduledAt   DATETIME        NOT NULL,
        CompletedAt   DATETIME        NULL,
        OrganizerId   INT             NULL,
        Participants  NVARCHAR(2048)  NULL,
        Platform      INT             NOT NULL CONSTRAINT DF_VideoConferences_Platform DEFAULT (0),
        MeetingUrl    NVARCHAR(512)   NULL,
        Notes         NVARCHAR(1024)  NULL,
        TicketId      INT             NULL,
        CONSTRAINT PK_dbo_VideoConferences PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.VideoConferences_dbo.Employees_OrganizerId]
            FOREIGN KEY (OrganizerId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.VideoConferences_dbo.Documents_TicketId]
            FOREIGN KEY (TicketId) REFERENCES dbo.Documents (Id)
    );
    CREATE NONCLUSTERED INDEX IX_VideoConferences_OrganizerId ON dbo.VideoConferences (OrganizerId);
    CREATE NONCLUSTERED INDEX IX_VideoConferences_TicketId    ON dbo.VideoConferences (TicketId);
    CREATE NONCLUSTERED INDEX IX_VideoConferences_ScheduledAt ON dbo.VideoConferences (ScheduledAt);
END
GO

/* ---------- 19. Phase 16 — Security & Admin -------------------------------- */
/* Колонки на Employees для срока действия пароля и lockout. */
IF COL_LENGTH(N'dbo.Employees', N'LastPasswordChangeAt') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD LastPasswordChangeAt DATETIME NULL;
END
GO

IF COL_LENGTH(N'dbo.Employees', N'LockedUntil') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD LockedUntil DATETIME NULL;
END
GO

/* OrganizationSettings — singleton (Id = 1, без IDENTITY: EF6-конфигурация
   использует DatabaseGeneratedOption.None, INSERT идёт с явным Id=1). */
IF OBJECT_ID(N'dbo.OrganizationSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrganizationSettings
    (
        Id                          INT             NOT NULL,
        EncryptionKey               NVARCHAR(512)   NULL,
        EncryptionKeyGeneratedAt    DATETIME        NULL,
        PasswordMinLength           INT             NOT NULL CONSTRAINT DF_OrgSettings_PwdMinLen          DEFAULT (8),
        PasswordExpiryDays          INT             NOT NULL CONSTRAINT DF_OrgSettings_PwdExpiryDays      DEFAULT (90),
        PasswordHistoryDepth        INT             NOT NULL CONSTRAINT DF_OrgSettings_PwdHistoryDepth    DEFAULT (5),
        LockoutFailureThreshold     INT             NOT NULL CONSTRAINT DF_OrgSettings_LockoutThreshold   DEFAULT (5),
        LockoutWindowMinutes        INT             NOT NULL CONSTRAINT DF_OrgSettings_LockoutWindow      DEFAULT (10),
        LockoutDurationMinutes      INT             NOT NULL CONSTRAINT DF_OrgSettings_LockoutDuration    DEFAULT (30),
        CONSTRAINT PK_dbo_OrganizationSettings PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* EmployeePasswordHistories — последние N паролей для запрета повторного использования. */
IF OBJECT_ID(N'dbo.EmployeePasswordHistories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeePasswordHistories
    (
        Id            INT             IDENTITY(1, 1) NOT NULL,
        EmployeeId    INT             NOT NULL,
        PasswordHash  NVARCHAR(512)   NOT NULL,
        SetAt         DATETIME        NOT NULL,
        CONSTRAINT PK_dbo_EmployeePasswordHistories PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.EmployeePasswordHistories_dbo.Employees_EmployeeId]
            FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_EmployeePasswordHistories_EmployeeId ON dbo.EmployeePasswordHistories (EmployeeId);
    CREATE NONCLUSTERED INDEX IX_EmployeePasswordHistories_SetAt      ON dbo.EmployeePasswordHistories (SetAt DESC);
END
GO

/* LoginAttempts — журнал попыток входа (success/failure + причина + IP).
   Колонка AttemptedFullName хранит ФИО, под которым была попытка входа,
   и совпадает по имени со свойством LoginAttempt.AttemptedFullName в EF. */
IF OBJECT_ID(N'dbo.LoginAttempts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoginAttempts
    (
        Id                INT             IDENTITY(1, 1) NOT NULL,
        EmployeeId        INT             NULL,
        AttemptedFullName NVARCHAR(256)   NULL,
        Timestamp         DATETIME        NOT NULL,
        IpAddress         NVARCHAR(64)    NULL,
        Success           BIT             NOT NULL CONSTRAINT DF_LoginAttempts_Success DEFAULT (0),
        FailureReason     INT             NOT NULL CONSTRAINT DF_LoginAttempts_FailureReason DEFAULT (0),
        CONSTRAINT PK_dbo_LoginAttempts PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.LoginAttempts_dbo.Employees_EmployeeId]
            FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_LoginAttempts_EmployeeId ON dbo.LoginAttempts (EmployeeId);
    CREATE NONCLUSTERED INDEX IX_LoginAttempts_Timestamp  ON dbo.LoginAttempts (Timestamp DESC);
END
GO

/* Seed singleton OrganizationSettings c дефолтами политики безопасности. */
IF NOT EXISTS (SELECT 1 FROM dbo.OrganizationSettings)
BEGIN
    INSERT INTO dbo.OrganizationSettings
        (EncryptionKey, EncryptionKeyGeneratedAt,
         PasswordMinLength, PasswordExpiryDays, PasswordHistoryDepth,
         LockoutFailureThreshold, LockoutWindowMinutes, LockoutDurationMinutes)
    VALUES
        (NULL, NULL, 8, 90, 5, 5, 10, 30);
END
GO

/* ---------- 20. (необязательно) лёгкий сид-набор для проверки ------------- */
/* Расскоментируй блок ниже, если хочешь сразу получить несколько строк.
 * Пароль 'password' берётся из DemoDataSeeder; реальный хеш выставляет
 * приложение при первом запуске — здесь оставляем PasswordHash = NULL,
 * чтобы не конфликтовать с PBKDF2-алгоритмом.

IF NOT EXISTS (SELECT 1 FROM dbo.Employees)
BEGIN
    -- Role: Admin=0, Manager=1, Archivist=2, TechSupport=3, WarehouseManager=4
    INSERT INTO dbo.Employees (FullName, [Position], [Role], PasswordHash) VALUES
        (N'Администратор МКУ АХУ БМР', N'Администратор информационной системы', 0, NULL),
        (N'Стерликов Дмитрий Николаевич', N'Руководитель службы по информационно-техническому обеспечению', 1, NULL),
        (N'Бурдина Галина Николаевна', N'Начальник архивного отдела', 2, NULL),
        (N'Дорофеев Артем Валерьевич', N'Специалист-техник по компьютерным сетям и системам', 3, NULL),
        (N'Зайченко Татьяна Александровна', N'Специалист-техник по компьютерным сетям и системам', 4, NULL);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Vehicles)
BEGIN
    INSERT INTO dbo.Vehicles (Model, LicensePlate, CurrentStatus) VALUES
        (N'Lada Largus', N'А123БВ 64', 0),
        (N'ГАЗель NEXT', N'В777ТТ 64', 0),
        (N'УАЗ Патриот', N'Е111КХ 64', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.InventoryItems)
BEGIN
    INSERT INTO dbo.InventoryItems ([Name], Category, TotalQuantity) VALUES
        (N'Бумага A4 для документооборота', 0, 50),
        (N'Картридж для оргтехники',        1,  4),
        (N'Средство для уборки помещений',  2,  3);
END
GO
*/

PRINT N'AhuErpDb: схема создана / актуальна.';
GO
