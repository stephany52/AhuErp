/* ============================================================================
 * AhuErp — создание схемы БД (SQL Server, T-SQL).
 *
 * Скрипт собран по всем EF6 Code-First миграциям в
 * `src/AhuErp.Core/Migrations` и приведён к текущему состоянию модели
 * (`src/AhuErp.Core/Data/AhuDbContext.cs`):
 *
 *    1) 20260423121238_InitialCreate
 *    2) 20260423125626_AddEmployeeAuth
 *    3) 20260423131841_AddInventoryAndItTicket
 *    4) 20260423175847_AddVehicleTripDriverName
 *    5) 20260426113552_AddArchiveRequestKind
 *    6) 20260426140000_AddEnterpriseEDMSFeatures              (Phase 7 — СЭД)
 *    7) 202604270000000_AddOrgAndSubstitution                 (Phase 11 — оргструктура)
 *    8) 202604280000000_AddNotifications                      (Phase 9  — уведомления)
 *    9) 202604290000000_AddSignatures                         (Phase 8  — подписи)
 *   10) 202604300000000_AddSearchIndex                        (Phase 10 — полнотекстовый поиск)
 *   11) 20260430202430_AddInventoryItemUnitAndMinimumBalance
 *   12) 20260507205200_AddNomenclatureCounters                (Phase 15)
 *   13) 20260508195200_AddItoExpansionPhase14                 (Phase 14 — ИТО)
 *   14) 20260509100000_AddRegistrationJournalsPhase15         (Phase 15 — журналы)
 *   15) 20260509150000_AddSecurityAndAdminPhase16             (Phase 16 — безопасность/админ)
 *   16) 20260510100000_AddVehicleOsagoWaybillPhase17          (Phase 17 — ТС/ОСАГО/путевой лист)
 *   17) 20260511100000_AddBuildingsMaintenancePhase18         (Phase 18 — здания/ОС)
 *
 * Запуск в SQL Server Management Studio:
 *   1. Подключиться к экземпляру SQL Server (например, DESKTOP-…\SQLEXPRESS).
 *   2. File → Open → create-db.sql → F5.
 *   3. Проверить:  SELECT name FROM sys.databases WHERE name = 'AhuErpDb';
 *
 * После создания схемы можно один раз накатить наполнение демо-данными:
 *   File → Open → seed-db.sql → F5  (см. `scripts/seed-db.sql`).
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

/* ---------- 1. Employees --------------------------------------------------- */
IF OBJECT_ID(N'dbo.Employees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees
    (
        Id                    INT            IDENTITY(1, 1) NOT NULL,
        FullName              NVARCHAR(256)  NOT NULL,
        [Position]            NVARCHAR(256)  NULL,
        [Role]                INT            NOT NULL CONSTRAINT DF_Employees_Role     DEFAULT (0),
        PasswordHash          NVARCHAR(512)  NULL,
        /* Phase 11 — оргструктура / e-mail / активность */
        Email                 NVARCHAR(256)  NULL,
        DepartmentId          INT            NULL,
        IsActive              BIT            NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT (1),
        TerminatedAt          DATETIME       NULL,
        /* Phase 16 — срок действия пароля и lockout */
        LastPasswordChangeAt  DATETIME       NULL,
        LockedUntil           DATETIME       NULL,
        CONSTRAINT PK_dbo_Employees PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* Phase 11 — добавляем колонки на уже существующих БД (идемпотентно). */
IF COL_LENGTH(N'dbo.Employees', N'Email') IS NULL
    ALTER TABLE dbo.Employees ADD Email NVARCHAR(256) NULL;
GO
IF COL_LENGTH(N'dbo.Employees', N'DepartmentId') IS NULL
    ALTER TABLE dbo.Employees ADD DepartmentId INT NULL;
GO
IF COL_LENGTH(N'dbo.Employees', N'IsActive') IS NULL
    ALTER TABLE dbo.Employees ADD IsActive BIT NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT (1);
GO
IF COL_LENGTH(N'dbo.Employees', N'TerminatedAt') IS NULL
    ALTER TABLE dbo.Employees ADD TerminatedAt DATETIME NULL;
GO
IF COL_LENGTH(N'dbo.Employees', N'LastPasswordChangeAt') IS NULL
    ALTER TABLE dbo.Employees ADD LastPasswordChangeAt DATETIME NULL;
GO
IF COL_LENGTH(N'dbo.Employees', N'LockedUntil') IS NULL
    ALTER TABLE dbo.Employees ADD LockedUntil DATETIME NULL;
GO

/* ---------- 1a. Departments (Phase 7 + Phase 11) -------------------------- */
IF OBJECT_ID(N'dbo.Departments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Departments
    (
        Id                  INT            IDENTITY(1, 1) NOT NULL,
        Name                NVARCHAR(256)  NOT NULL,
        ShortCode           NVARCHAR(16)   NULL,
        IsActive            BIT            NOT NULL CONSTRAINT DF_Departments_IsActive DEFAULT (1),
        /* Phase 11 — иерархия и руководитель отдела */
        ParentDepartmentId  INT            NULL,
        HeadEmployeeId      INT            NULL,
        CONSTRAINT PK_dbo_Departments PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* Phase 11 — добавляем колонки иерархии на старых БД. */
IF COL_LENGTH(N'dbo.Departments', N'ParentDepartmentId') IS NULL
    ALTER TABLE dbo.Departments ADD ParentDepartmentId INT NULL;
GO
IF COL_LENGTH(N'dbo.Departments', N'HeadEmployeeId') IS NULL
    ALTER TABLE dbo.Departments ADD HeadEmployeeId INT NULL;
GO

/* FK Departments.ParentDepartmentId → Departments.Id (без каскада). */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE [name] = N'FK_dbo.Departments_dbo.Departments_ParentDepartmentId')
BEGIN
    ALTER TABLE dbo.Departments
        ADD CONSTRAINT [FK_dbo.Departments_dbo.Departments_ParentDepartmentId]
        FOREIGN KEY (ParentDepartmentId)
        REFERENCES dbo.Departments (Id);
END
GO

/* FK Departments.HeadEmployeeId → Employees.Id (без каскада). */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE [name] = N'FK_dbo.Departments_dbo.Employees_HeadEmployeeId')
BEGIN
    ALTER TABLE dbo.Departments
        ADD CONSTRAINT [FK_dbo.Departments_dbo.Employees_HeadEmployeeId]
        FOREIGN KEY (HeadEmployeeId)
        REFERENCES dbo.Employees (Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE [name] = N'IX_Departments_ParentDepartmentId' AND object_id = OBJECT_ID(N'dbo.Departments'))
    CREATE NONCLUSTERED INDEX IX_Departments_ParentDepartmentId ON dbo.Departments (ParentDepartmentId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE [name] = N'IX_Departments_HeadEmployeeId' AND object_id = OBJECT_ID(N'dbo.Departments'))
    CREATE NONCLUSTERED INDEX IX_Departments_HeadEmployeeId ON dbo.Departments (HeadEmployeeId);
GO

/* FK Employees.DepartmentId → Departments.Id (после создания Departments). */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE [name] = N'FK_dbo.Employees_dbo.Departments_DepartmentId')
BEGIN
    ALTER TABLE dbo.Employees
        ADD CONSTRAINT [FK_dbo.Employees_dbo.Departments_DepartmentId]
        FOREIGN KEY (DepartmentId)
        REFERENCES dbo.Departments (Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE [name] = N'IX_Employees_DepartmentId' AND object_id = OBJECT_ID(N'dbo.Employees'))
    CREATE NONCLUSTERED INDEX IX_Employees_DepartmentId ON dbo.Employees (DepartmentId);
GO

/* ---------- 1b. DocumentTypeRefs (Phase 7) -------------------------------- */
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

/* ---------- 1c. NomenclatureCases (Phase 7) ------------------------------- */
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
        CONSTRAINT PK_dbo_NomenclatureCases PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.NomenclatureCases_dbo.Departments_DepartmentId]
            FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (Id)
    );
    CREATE NONCLUSTERED INDEX IX_NomenclatureCases_DepartmentId
        ON dbo.NomenclatureCases (DepartmentId);
END
GO

/* ---------- 1d. NomenclatureCounters (Phase 15 / Improvement #12) --------- */
IF OBJECT_ID(N'dbo.NomenclatureCounters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NomenclatureCounters
    (
        Id          INT           IDENTITY(1, 1) NOT NULL,
        TypeCode    NVARCHAR(16)  NOT NULL,
        [Year]      INT           NOT NULL,
        LastNumber  INT           NOT NULL,
        CONSTRAINT PK_dbo_NomenclatureCounters PRIMARY KEY CLUSTERED (Id ASC)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UX_NomenclatureCounter_TypeCode_Year
        ON dbo.NomenclatureCounters (TypeCode, [Year]);
END
GO

/* ---------- 2. Documents (TPH: Document / ArchiveRequest / ItTicket) ------ */
IF OBJECT_ID(N'dbo.Documents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Documents
    (
        Id                          INT             IDENTITY(1, 1) NOT NULL,
        [Type]                      INT             NOT NULL,
        Direction                   INT             NOT NULL CONSTRAINT DF_Documents_Direction      DEFAULT (0),
        AccessLevel                 INT             NOT NULL CONSTRAINT DF_Documents_AccessLevel    DEFAULT (0),
        RegistrationNumber          NVARCHAR(64)    NULL,
        RegistrationDate            DATETIME        NULL,
        DocumentTypeRefId           INT             NULL,
        NomenclatureCaseId          INT             NULL,
        AuthorId                    INT             NULL,
        Title                       NVARCHAR(512)   NOT NULL,
        Summary                     NVARCHAR(4000)  NULL,
        Correspondent               NVARCHAR(512)   NULL,
        IncomingNumber              NVARCHAR(64)    NULL,
        IncomingDate                DATETIME        NULL,
        CreationDate                DATETIME        NOT NULL,
        Deadline                    DATETIME        NOT NULL,
        [Status]                    INT             NOT NULL,
        AssignedEmployeeId          INT             NULL,
        BasisDocumentId             INT             NULL,
        ApprovalStatus              INT             NOT NULL CONSTRAINT DF_Documents_ApprovalStatus DEFAULT (0),
        HasPassportScan             BIT             NULL,
        HasWorkBookScan             BIT             NULL,
        ArchiveRequestKind          INT             NULL,
        AffectedEquipment           NVARCHAR(256)   NULL,
        ResolutionNotes             NVARCHAR(1024)  NULL,
        /* Phase 14 — расширение модуля ИТО (FK на Equipment.Id ставится ниже) */
        AffectedEquipmentId         INT             NULL,
        Kind                        INT             NULL,
        IsSentToVendor              BIT             NULL,
        VendorName                  NVARCHAR(256)   NULL,
        VendorTicketNumber          NVARCHAR(64)    NULL,
        VendorReturnDeadline        DATETIME        NULL,
        CompletedAt                 DATETIME        NULL,
        /* Phase 8 — подпись и блокировка */
        IsLocked                    BIT             NOT NULL CONSTRAINT DF_Documents_IsLocked       DEFAULT (0),
        CurrentVersionAttachmentId  INT             NULL,
        DocumentDiscriminator       NVARCHAR(128)   NOT NULL,
        CONSTRAINT PK_dbo_Documents PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.Documents_dbo.Employees_AssignedEmployeeId]
            FOREIGN KEY (AssignedEmployeeId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.Documents_dbo.Employees_AuthorId]
            FOREIGN KEY (AuthorId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.Documents_dbo.DocumentTypeRefs_DocumentTypeRefId]
            FOREIGN KEY (DocumentTypeRefId) REFERENCES dbo.DocumentTypeRefs (Id),
        CONSTRAINT [FK_dbo.Documents_dbo.NomenclatureCases_NomenclatureCaseId]
            FOREIGN KEY (NomenclatureCaseId) REFERENCES dbo.NomenclatureCases (Id),
        CONSTRAINT [FK_dbo.Documents_dbo.Documents_BasisDocumentId]
            FOREIGN KEY (BasisDocumentId) REFERENCES dbo.Documents (Id)
    );
    CREATE NONCLUSTERED INDEX IX_Documents_AssignedEmployeeId ON dbo.Documents (AssignedEmployeeId);
    CREATE NONCLUSTERED INDEX IX_Documents_AuthorId           ON dbo.Documents (AuthorId);
    CREATE NONCLUSTERED INDEX IX_Documents_DocumentTypeRefId  ON dbo.Documents (DocumentTypeRefId);
    CREATE NONCLUSTERED INDEX IX_Documents_NomenclatureCaseId ON dbo.Documents (NomenclatureCaseId);
    CREATE NONCLUSTERED INDEX IX_Documents_BasisDocumentId    ON dbo.Documents (BasisDocumentId);
END
GO

/* Phase 8 — добавляем колонки на старых БД. */
IF COL_LENGTH(N'dbo.Documents', N'IsLocked') IS NULL
    ALTER TABLE dbo.Documents ADD IsLocked BIT NOT NULL CONSTRAINT DF_Documents_IsLocked DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.Documents', N'CurrentVersionAttachmentId') IS NULL
    ALTER TABLE dbo.Documents ADD CurrentVersionAttachmentId INT NULL;
GO

/* Phase 14 — Kind / IsSentToVendor для TPH ItTicket по миграции
   AddItoExpansionPhase14 должны быть NULLable (EF6 conventions для подкласса).
   В ранних версиях этого скрипта они были созданы как NOT NULL DEFAULT (0) —
   приводим к актуальному виду на старых БД, попутно убирая default-constraint. */
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE [name] = N'DF_Documents_Kind')
    ALTER TABLE dbo.Documents DROP CONSTRAINT DF_Documents_Kind;
GO
IF COLUMNPROPERTY(OBJECT_ID(N'dbo.Documents'), N'Kind', 'AllowsNull') = 0
    ALTER TABLE dbo.Documents ALTER COLUMN Kind INT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE [name] = N'DF_Documents_IsSentToVendor')
    ALTER TABLE dbo.Documents DROP CONSTRAINT DF_Documents_IsSentToVendor;
GO
IF COLUMNPROPERTY(OBJECT_ID(N'dbo.Documents'), N'IsSentToVendor', 'AllowsNull') = 0
    ALTER TABLE dbo.Documents ALTER COLUMN IsSentToVendor BIT NULL;
GO

/* ---------- 3. Vehicles --------------------------------------------------- */
IF OBJECT_ID(N'dbo.Vehicles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Vehicles
    (
        Id                       INT            IDENTITY(1, 1) NOT NULL,
        Model                    NVARCHAR(128)  NOT NULL,
        LicensePlate             NVARCHAR(32)   NOT NULL,
        CurrentStatus            INT            NOT NULL,
        /* Phase 15 — учёт ГСМ */
        FuelType                 INT            NOT NULL CONSTRAINT DF_Vehicles_FuelType                DEFAULT (0),
        FuelConsumptionPer100Km  DECIMAL(7, 2)  NOT NULL CONSTRAINT DF_Vehicles_FuelConsumptionPer100Km DEFAULT (0),
        /* Phase 17 — паспортные данные ТС, ОСАГО / ТО */
        VehicleClass             INT            NOT NULL CONSTRAINT DF_Vehicles_VehicleClass            DEFAULT (0),
        Make                     NVARCHAR(64)   NULL,
        [Year]                   INT            NOT NULL CONSTRAINT DF_Vehicles_Year                    DEFAULT (0),
        Vin                      NVARCHAR(32)   NULL,
        OdometerCurrent          INT            NULL,
        NextMaintenanceOdometer  INT            NULL,
        OsagoExpiry              DATETIME       NULL,
        TechInspectionExpiry     DATETIME       NULL,
        CONSTRAINT PK_dbo_Vehicles PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* Phase 15 — учёт ГСМ на старых БД. */
IF COL_LENGTH(N'dbo.Vehicles', N'FuelType') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles
        ADD FuelType                 INT             NOT NULL CONSTRAINT DF_Vehicles_FuelType                DEFAULT (0),
            FuelConsumptionPer100Km  DECIMAL(7, 2)   NOT NULL CONSTRAINT DF_Vehicles_FuelConsumptionPer100Km DEFAULT (0);
END
GO

/* Phase 17 — ОСАГО / ТО / путевой лист на старых БД. */
IF COL_LENGTH(N'dbo.Vehicles', N'VehicleClass') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles
        ADD VehicleClass             INT             NOT NULL CONSTRAINT DF_Vehicles_VehicleClass DEFAULT (0),
            Make                     NVARCHAR(64)    NULL,
            [Year]                   INT             NOT NULL CONSTRAINT DF_Vehicles_Year         DEFAULT (0),
            Vin                      NVARCHAR(32)    NULL,
            OdometerCurrent          INT             NULL,
            NextMaintenanceOdometer  INT             NULL,
            OsagoExpiry              DATETIME        NULL,
            TechInspectionExpiry     DATETIME        NULL;
END
GO

/* ---------- 4. VehicleTrips ----------------------------------------------- */
IF OBJECT_ID(N'dbo.VehicleTrips', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleTrips
    (
        Id                INT             IDENTITY(1, 1) NOT NULL,
        VehicleId         INT             NOT NULL,
        StartDate         DATETIME        NOT NULL,
        EndDate           DATETIME        NOT NULL,
        DocumentId        INT             NULL,
        DriverName        NVARCHAR(128)   NULL,
        BasisDocumentId   INT             NULL,
        /* Phase 15 — учёт ГСМ и фактических часов */
        OdometerStart     INT             NULL,
        OdometerEnd       INT             NULL,
        FuelIssuedLiters  DECIMAL(9, 2)   NULL,
        Route             NVARCHAR(512)   NULL,
        PassengerNames    NVARCHAR(1024)  NULL,
        ActualStart       DATETIME        NULL,
        ActualEnd         DATETIME        NULL,
        CONSTRAINT PK_dbo_VehicleTrips PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.VehicleTrips_dbo.Vehicles_VehicleId]
            FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles (Id) ON DELETE CASCADE,
        CONSTRAINT [FK_dbo.VehicleTrips_dbo.Documents_DocumentId]
            FOREIGN KEY (DocumentId) REFERENCES dbo.Documents (Id),
        CONSTRAINT [FK_dbo.VehicleTrips_dbo.Documents_BasisDocumentId]
            FOREIGN KEY (BasisDocumentId) REFERENCES dbo.Documents (Id)
    );
    CREATE NONCLUSTERED INDEX IX_VehicleTrips_VehicleId       ON dbo.VehicleTrips (VehicleId);
    CREATE NONCLUSTERED INDEX IX_VehicleTrips_DocumentId      ON dbo.VehicleTrips (DocumentId);
    CREATE NONCLUSTERED INDEX IX_VehicleTrips_BasisDocumentId ON dbo.VehicleTrips (BasisDocumentId);
END
GO

/* Phase 7 — BasisDocumentId на VehicleTrips для старых БД. */
IF COL_LENGTH(N'dbo.VehicleTrips', N'BasisDocumentId') IS NULL
BEGIN
    ALTER TABLE dbo.VehicleTrips ADD BasisDocumentId INT NULL;
    ALTER TABLE dbo.VehicleTrips
        ADD CONSTRAINT [FK_dbo.VehicleTrips_dbo.Documents_BasisDocumentId]
        FOREIGN KEY (BasisDocumentId) REFERENCES dbo.Documents (Id);
    CREATE NONCLUSTERED INDEX IX_VehicleTrips_BasisDocumentId ON dbo.VehicleTrips (BasisDocumentId);
END
GO

/* Phase 15 — учёт ГСМ на старых БД. */
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
        Id              INT            IDENTITY(1, 1) NOT NULL,
        [Name]          NVARCHAR(256)  NOT NULL,
        Category        INT            NOT NULL,
        TotalQuantity   INT            NOT NULL,
        Unit            NVARCHAR(32)   NULL,
        MinimumBalance  INT            NOT NULL CONSTRAINT DF_InventoryItems_MinimumBalance DEFAULT (0),
        CONSTRAINT PK_dbo_InventoryItems PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* AddInventoryItemUnitAndMinimumBalance — добавляем колонки на старых БД. */
IF COL_LENGTH(N'dbo.InventoryItems', N'Unit') IS NULL
    ALTER TABLE dbo.InventoryItems ADD Unit NVARCHAR(32) NULL;
GO
IF COL_LENGTH(N'dbo.InventoryItems', N'MinimumBalance') IS NULL
    ALTER TABLE dbo.InventoryItems ADD MinimumBalance INT NOT NULL CONSTRAINT DF_InventoryItems_MinimumBalance DEFAULT (0);
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
        BasisDocumentId  INT       NULL,
        CONSTRAINT PK_dbo_InventoryTransactions PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.InventoryTransactions_dbo.InventoryItems_InventoryItemId]
            FOREIGN KEY (InventoryItemId) REFERENCES dbo.InventoryItems (Id) ON DELETE CASCADE,
        CONSTRAINT [FK_dbo.InventoryTransactions_dbo.Documents_DocumentId]
            FOREIGN KEY (DocumentId) REFERENCES dbo.Documents (Id),
        CONSTRAINT [FK_dbo.InventoryTransactions_dbo.Employees_InitiatorId]
            FOREIGN KEY (InitiatorId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.InventoryTransactions_dbo.Documents_BasisDocumentId]
            FOREIGN KEY (BasisDocumentId) REFERENCES dbo.Documents (Id)
    );
    CREATE NONCLUSTERED INDEX IX_InventoryTransactions_InventoryItemId ON dbo.InventoryTransactions (InventoryItemId);
    CREATE NONCLUSTERED INDEX IX_InventoryTransactions_DocumentId      ON dbo.InventoryTransactions (DocumentId);
    CREATE NONCLUSTERED INDEX IX_InventoryTransactions_InitiatorId     ON dbo.InventoryTransactions (InitiatorId);
    CREATE NONCLUSTERED INDEX IX_InventoryTransactions_BasisDocumentId ON dbo.InventoryTransactions (BasisDocumentId);
END
GO

/* Phase 7 — BasisDocumentId на InventoryTransactions для старых БД. */
IF COL_LENGTH(N'dbo.InventoryTransactions', N'BasisDocumentId') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryTransactions ADD BasisDocumentId INT NULL;
    ALTER TABLE dbo.InventoryTransactions
        ADD CONSTRAINT [FK_dbo.InventoryTransactions_dbo.Documents_BasisDocumentId]
        FOREIGN KEY (BasisDocumentId) REFERENCES dbo.Documents (Id);
    CREATE NONCLUSTERED INDEX IX_InventoryTransactions_BasisDocumentId ON dbo.InventoryTransactions (BasisDocumentId);
END
GO

/* ---------- 7. DocumentCaseLinks (Phase 7) -------------------------------- */
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

/* ---------- 8. DocumentAttachments (Phase 7) ------------------------------ */
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

/* Phase 8 — FK Documents.CurrentVersionAttachmentId → DocumentAttachments.Id
   ставится после создания DocumentAttachments. */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE [name] = N'FK_dbo.Documents_dbo.DocumentAttachments_CurrentVersionAttachmentId')
BEGIN
    ALTER TABLE dbo.Documents
        ADD CONSTRAINT [FK_dbo.Documents_dbo.DocumentAttachments_CurrentVersionAttachmentId]
        FOREIGN KEY (CurrentVersionAttachmentId) REFERENCES dbo.DocumentAttachments (Id);
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE [name] = N'IX_Documents_CurrentVersionAttachment' AND object_id = OBJECT_ID(N'dbo.Documents'))
    CREATE NONCLUSTERED INDEX IX_Documents_CurrentVersionAttachment ON dbo.Documents (CurrentVersionAttachmentId);
GO

/* ---------- 8a. DocumentSignatures (Phase 8) ------------------------------ */
IF OBJECT_ID(N'dbo.DocumentSignatures', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentSignatures
    (
        Id                    INT             IDENTITY(1, 1) NOT NULL,
        DocumentId            INT             NOT NULL,
        AttachmentId          INT             NULL,
        SignerId              INT             NOT NULL,
        Kind                  INT             NOT NULL,
        SignedAt              DATETIME        NOT NULL,
        SignedHash            NVARCHAR(128)   NULL,
        SignatureBlobBase64   NVARCHAR(MAX)   NULL,
        CertificateThumbprint NVARCHAR(512)   NULL,
        CertificateSubject    NVARCHAR(256)   NULL,
        CertificateNotAfter   DATETIME        NULL,
        Reason                NVARCHAR(1024)  NULL,
        IsRevoked             BIT             NOT NULL CONSTRAINT DF_DocumentSignatures_IsRevoked DEFAULT (0),
        RevokedAt             DATETIME        NULL,
        CONSTRAINT PK_dbo_DocumentSignatures PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.DocumentSignatures_dbo.Documents_DocumentId]
            FOREIGN KEY (DocumentId) REFERENCES dbo.Documents (Id),
        CONSTRAINT [FK_dbo.DocumentSignatures_dbo.DocumentAttachments_AttachmentId]
            FOREIGN KEY (AttachmentId) REFERENCES dbo.DocumentAttachments (Id),
        CONSTRAINT [FK_dbo.DocumentSignatures_dbo.Employees_SignerId]
            FOREIGN KEY (SignerId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_DocumentSignatures_Document_Active
        ON dbo.DocumentSignatures (DocumentId, IsRevoked);
    CREATE NONCLUSTERED INDEX IX_DocumentSignatures_Attachment
        ON dbo.DocumentSignatures (AttachmentId);
    CREATE NONCLUSTERED INDEX IX_DocumentSignatures_Signer
        ON dbo.DocumentSignatures (SignerId);
END
GO

/* ---------- 9. DocumentResolutions (Phase 7) ------------------------------ */
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

/* ---------- 10. DocumentTasks (Phase 7) ----------------------------------- */
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

/* ---------- 11. ApprovalRouteTemplates (Phase 7) -------------------------- */
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

/* ---------- 12. ApprovalStages (Phase 7) ---------------------------------- */
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

/* ---------- 13. DocumentApprovals (Phase 7) ------------------------------- */
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
        Decision      INT             NOT NULL CONSTRAINT DF_DocumentApprovals_Decision   DEFAULT (0),
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

/* ---------- 13a. SafetyBriefings (Phase 15) ------------------------------- */
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
    CREATE NONCLUSTERED INDEX IX_SafetyBriefings_TraineeEmployeeId    ON dbo.SafetyBriefings (TraineeEmployeeId);
    CREATE NONCLUSTERED INDEX IX_SafetyBriefings_InstructorEmployeeId ON dbo.SafetyBriefings (InstructorEmployeeId);
    CREATE NONCLUSTERED INDEX IX_SafetyBriefings_Date_Kind            ON dbo.SafetyBriefings (BriefingDate, Kind);
END
GO

/* ---------- 13b. Inventarizations (Phase 15) ------------------------------ */
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
    CREATE NONCLUSTERED INDEX IX_Inventarizations_ChairmanId       ON dbo.Inventarizations (ChairmanId);
    CREATE NONCLUSTERED INDEX IX_Inventarizations_ResultDocumentId ON dbo.Inventarizations (ResultDocumentId);
    CREATE NONCLUSTERED INDEX IX_Inventarizations_Date_Scope       ON dbo.Inventarizations (StartDate, Scope);
END
GO

/* ---------- 13c. InventarizationDiscrepancies (Phase 15) ------------------ */
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

/* ---------- 13d. ArchiveTransfers (Phase 15) ------------------------------ */
IF OBJECT_ID(N'dbo.ArchiveTransfers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArchiveTransfers
    (
        Id                  INT             IDENTITY(1, 1) NOT NULL,
        NomenclatureCaseId  INT             NOT NULL,
        TransferDate        DATETIME        NOT NULL,
        TransferredById     INT             NULL,
        AcceptedById        INT             NULL,
        ActDocumentId       INT             NULL,
        ArchiveCode         NVARCHAR(64)    NULL,
        RetentionYears      INT             NOT NULL,
        Notes               NVARCHAR(2048)  NULL,
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
    CREATE NONCLUSTERED INDEX IX_ArchiveTransfers_NomenclatureCaseId ON dbo.ArchiveTransfers (NomenclatureCaseId);
    CREATE NONCLUSTERED INDEX IX_ArchiveTransfers_TransferredById    ON dbo.ArchiveTransfers (TransferredById);
    CREATE NONCLUSTERED INDEX IX_ArchiveTransfers_AcceptedById       ON dbo.ArchiveTransfers (AcceptedById);
    CREATE NONCLUSTERED INDEX IX_ArchiveTransfers_ActDocumentId      ON dbo.ArchiveTransfers (ActDocumentId);
    CREATE NONCLUSTERED INDEX IX_ArchiveTransfers_Date               ON dbo.ArchiveTransfers (TransferDate);
END
GO

/* ---------- 14. AuditLogs (Phase 7) --------------------------------------- */
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
        Id           INT            IDENTITY(1, 1) NOT NULL,
        [Name]       NVARCHAR(128)  NOT NULL,
        Vlan         NVARCHAR(16)   NULL,
        IpRange      NVARCHAR(32)   NULL,
        SubnetMask   NVARCHAR(32)   NULL,
        Gateway      NVARCHAR(32)   NULL,
        Dns          NVARCHAR(128)  NULL,
        Notes        NVARCHAR(512)  NULL,
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
        [Type]                 INT             NOT NULL CONSTRAINT DF_Equipment_Type   DEFAULT (0),
        Model                  NVARCHAR(256)   NULL,
        SerialNumber           NVARCHAR(64)    NULL,
        MacAddress             NVARCHAR(32)    NULL,
        IpAddress              NVARCHAR(32)    NULL,
        Room                   NVARCHAR(64)    NULL,
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
END
GO

/* Phase 14 — FK Documents.AffectedEquipmentId → Equipment.Id (после Equipment). */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE [name] = N'FK_dbo.Documents_dbo.Equipment_AffectedEquipmentId')
BEGIN
    ALTER TABLE dbo.Documents
        ADD CONSTRAINT [FK_dbo.Documents_dbo.Equipment_AffectedEquipmentId]
        FOREIGN KEY (AffectedEquipmentId) REFERENCES dbo.Equipment (Id);
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE [name] = N'IX_Documents_AffectedEquipmentId' AND object_id = OBJECT_ID(N'dbo.Documents'))
    CREATE NONCLUSTERED INDEX IX_Documents_AffectedEquipmentId ON dbo.Documents (AffectedEquipmentId);
GO

/* ---------- 17. ItTicketDiagnosticEntries (Phase 14) ---------------------- */
IF OBJECT_ID(N'dbo.ItTicketDiagnosticEntries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItTicketDiagnosticEntries
    (
        Id         INT             IDENTITY(1, 1) NOT NULL,
        TicketId   INT             NOT NULL,
        AuthorId   INT             NOT NULL,
        Timestamp  DATETIME        NOT NULL,
        Action     NVARCHAR(1024)  NOT NULL,
        Category   NVARCHAR(64)    NULL,
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
        TicketId      INT             NULL,
        Topic         NVARCHAR(256)   NOT NULL,
        ScheduledAt   DATETIME        NOT NULL,
        CompletedAt   DATETIME        NULL,
        OrganizerId   INT             NOT NULL,
        Participants  NVARCHAR(2048)  NULL,
        Platform      INT             NOT NULL CONSTRAINT DF_VideoConferences_Platform DEFAULT (0),
        MeetingUrl    NVARCHAR(1024)  NULL,
        Notes         NVARCHAR(512)   NULL,
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

/* ---------- 19. Phase 16 — Security & Admin ------------------------------- */
/* OrganizationSettings — singleton (Id = 1, без IDENTITY). */
IF OBJECT_ID(N'dbo.OrganizationSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrganizationSettings
    (
        Id                          INT             NOT NULL,
        EncryptionKey               NVARCHAR(128)   NULL,
        EncryptionKeyGeneratedAt    DATETIME        NULL,
        PasswordMinLength           INT             NOT NULL CONSTRAINT DF_OrgSettings_PwdMinLen        DEFAULT (8),
        PasswordExpiryDays          INT             NOT NULL CONSTRAINT DF_OrgSettings_PwdExpiryDays    DEFAULT (90),
        PasswordHistoryDepth        INT             NOT NULL CONSTRAINT DF_OrgSettings_PwdHistoryDepth  DEFAULT (5),
        LockoutFailureThreshold     INT             NOT NULL CONSTRAINT DF_OrgSettings_LockoutThreshold DEFAULT (5),
        LockoutWindowMinutes        INT             NOT NULL CONSTRAINT DF_OrgSettings_LockoutWindow    DEFAULT (10),
        LockoutDurationMinutes      INT             NOT NULL CONSTRAINT DF_OrgSettings_LockoutDuration  DEFAULT (30),
        CONSTRAINT PK_dbo_OrganizationSettings PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

/* EmployeePasswordHistories — последние N паролей. */
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

/* LoginAttempts — журнал попыток входа. */
IF OBJECT_ID(N'dbo.LoginAttempts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoginAttempts
    (
        Id                INT             IDENTITY(1, 1) NOT NULL,
        EmployeeId        INT             NULL,
        AttemptedFullName NVARCHAR(256)   NULL,
        Timestamp         DATETIME        NOT NULL,
        IpAddress         NVARCHAR(64)    NULL,
        Success           BIT             NOT NULL CONSTRAINT DF_LoginAttempts_Success       DEFAULT (0),
        FailureReason     INT             NOT NULL CONSTRAINT DF_LoginAttempts_FailureReason DEFAULT (0),
        CONSTRAINT PK_dbo_LoginAttempts PRIMARY KEY CLUSTERED (Id ASC),
        /* WillCascadeOnDelete(false) в AhuDbContext — журнал попыток входа
           переживает удаление сотрудника как security audit trail. */
        CONSTRAINT [FK_dbo.LoginAttempts_dbo.Employees_EmployeeId]
            FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_LoginAttempts_EmployeeId ON dbo.LoginAttempts (EmployeeId);
    CREATE NONCLUSTERED INDEX IX_LoginAttempts_Timestamp  ON dbo.LoginAttempts (Timestamp DESC);
END
GO

/* Seed singleton OrganizationSettings c дефолтами политики безопасности. */
IF NOT EXISTS (SELECT 1 FROM dbo.OrganizationSettings)
BEGIN
    INSERT INTO dbo.OrganizationSettings
        (Id, EncryptionKey, EncryptionKeyGeneratedAt,
         PasswordMinLength, PasswordExpiryDays, PasswordHistoryDepth,
         LockoutFailureThreshold, LockoutWindowMinutes, LockoutDurationMinutes)
    VALUES
        (1, NULL, NULL, 8, 90, 5, 5, 10, 30);
END
GO

/* ---------- 20. Phase 18 — Buildings & Maintenance ------------------------ */
IF OBJECT_ID(N'dbo.Buildings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Buildings
    (
        Id                      INT             IDENTITY(1, 1) NOT NULL,
        Name                    NVARCHAR(128)   NOT NULL,
        Address                 NVARCHAR(256)   NULL,
        TotalAreaSqm            DECIMAL(10, 2)  NOT NULL CONSTRAINT DF_Buildings_TotalAreaSqm     DEFAULT (0),
        FloorCount              INT             NOT NULL CONSTRAINT DF_Buildings_FloorCount       DEFAULT (0),
        CommissionedYear        INT             NOT NULL CONSTRAINT DF_Buildings_CommissionedYear DEFAULT (0),
        ResponsibleEmployeeId   INT             NULL,
        Notes                   NVARCHAR(2048)  NULL,
        CONSTRAINT PK_dbo_Buildings PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.Buildings_dbo.Employees_ResponsibleEmployeeId]
            FOREIGN KEY (ResponsibleEmployeeId) REFERENCES dbo.Employees (Id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX IX_Buildings_Name              ON dbo.Buildings (Name);
    CREATE NONCLUSTERED INDEX IX_Buildings_ResponsibleEmployeeId    ON dbo.Buildings (ResponsibleEmployeeId);
END
GO

IF OBJECT_ID(N'dbo.Rooms', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Rooms
    (
        Id                      INT             IDENTITY(1, 1) NOT NULL,
        BuildingId              INT             NOT NULL,
        Number                  NVARCHAR(32)    NOT NULL,
        Floor                   INT             NOT NULL CONSTRAINT DF_Rooms_Floor    DEFAULT (0),
        AreaSqm                 DECIMAL(10, 2)  NOT NULL CONSTRAINT DF_Rooms_AreaSqm  DEFAULT (0),
        Purpose                 INT             NOT NULL CONSTRAINT DF_Rooms_Purpose  DEFAULT (1),
        ResponsibleEmployeeId   INT             NULL,
        Notes                   NVARCHAR(1024)  NULL,
        CONSTRAINT PK_dbo_Rooms PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.Rooms_dbo.Buildings_BuildingId]
            FOREIGN KEY (BuildingId) REFERENCES dbo.Buildings (Id) ON DELETE CASCADE,
        CONSTRAINT [FK_dbo.Rooms_dbo.Employees_ResponsibleEmployeeId]
            FOREIGN KEY (ResponsibleEmployeeId) REFERENCES dbo.Employees (Id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX IX_Rooms_Building_Number ON dbo.Rooms (BuildingId, Number);
    CREATE NONCLUSTERED INDEX IX_Rooms_ResponsibleEmployeeId ON dbo.Rooms (ResponsibleEmployeeId);
END
GO

IF OBJECT_ID(N'dbo.MaintenanceRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MaintenanceRequests
    (
        Id                      INT             IDENTITY(1, 1) NOT NULL,
        RegistrationDate        DATETIME        NOT NULL,
        BuildingId              INT             NOT NULL,
        RoomId                  INT             NULL,
        RequesterEmployeeId     INT             NOT NULL,
        Kind                    INT             NOT NULL CONSTRAINT DF_MaintenanceRequests_Kind     DEFAULT (0),
        Priority                INT             NOT NULL CONSTRAINT DF_MaintenanceRequests_Priority DEFAULT (1),
        Status                  INT             NOT NULL CONSTRAINT DF_MaintenanceRequests_Status   DEFAULT (0),
        Description             NVARCHAR(2048)  NOT NULL,
        AssigneeEmployeeId      INT             NULL,
        CompletedAt             DATETIME        NULL,
        Resolution              NVARCHAR(2048)  NULL,
        LinkedDocumentId        INT             NULL,
        CONSTRAINT PK_dbo_MaintenanceRequests PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.MaintenanceRequests_dbo.Buildings_BuildingId]
            FOREIGN KEY (BuildingId) REFERENCES dbo.Buildings (Id),
        CONSTRAINT [FK_dbo.MaintenanceRequests_dbo.Rooms_RoomId]
            FOREIGN KEY (RoomId) REFERENCES dbo.Rooms (Id),
        CONSTRAINT [FK_dbo.MaintenanceRequests_dbo.Employees_RequesterEmployeeId]
            FOREIGN KEY (RequesterEmployeeId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.MaintenanceRequests_dbo.Employees_AssigneeEmployeeId]
            FOREIGN KEY (AssigneeEmployeeId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.MaintenanceRequests_dbo.Documents_LinkedDocumentId]
            FOREIGN KEY (LinkedDocumentId) REFERENCES dbo.Documents (Id)
    );
    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_BuildingId          ON dbo.MaintenanceRequests (BuildingId);
    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_RoomId              ON dbo.MaintenanceRequests (RoomId);
    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_RequesterEmployeeId ON dbo.MaintenanceRequests (RequesterEmployeeId);
    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_AssigneeEmployeeId  ON dbo.MaintenanceRequests (AssigneeEmployeeId);
    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_LinkedDocumentId    ON dbo.MaintenanceRequests (LinkedDocumentId);
    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_Status              ON dbo.MaintenanceRequests (Status);
    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_RegistrationDate    ON dbo.MaintenanceRequests (RegistrationDate DESC);
END
GO

IF OBJECT_ID(N'dbo.FixedAssets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FixedAssets
    (
        Id                      INT             IDENTITY(1, 1) NOT NULL,
        InventoryNumber         NVARCHAR(64)    NOT NULL,
        Name                    NVARCHAR(256)   NOT NULL,
        Category                INT             NOT NULL CONSTRAINT DF_FixedAssets_Category        DEFAULT (0),
        Status                  INT             NOT NULL CONSTRAINT DF_FixedAssets_Status          DEFAULT (0),
        AcquisitionDate         DATETIME        NULL,
        AcquisitionCost         DECIMAL(18, 2)  NOT NULL CONSTRAINT DF_FixedAssets_AcquisitionCost DEFAULT (0),
        BookValue               DECIMAL(18, 2)  NOT NULL CONSTRAINT DF_FixedAssets_BookValue       DEFAULT (0),
        BuildingId              INT             NULL,
        RoomId                  INT             NULL,
        ResponsibleEmployeeId   INT             NULL,
        DecommissionedAt        DATETIME        NULL,
        DecommissionDocumentId  INT             NULL,
        Notes                   NVARCHAR(2048)  NULL,
        CONSTRAINT PK_dbo_FixedAssets PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.FixedAssets_dbo.Buildings_BuildingId]
            FOREIGN KEY (BuildingId) REFERENCES dbo.Buildings (Id),
        CONSTRAINT [FK_dbo.FixedAssets_dbo.Rooms_RoomId]
            FOREIGN KEY (RoomId) REFERENCES dbo.Rooms (Id),
        CONSTRAINT [FK_dbo.FixedAssets_dbo.Employees_ResponsibleEmployeeId]
            FOREIGN KEY (ResponsibleEmployeeId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.FixedAssets_dbo.Documents_DecommissionDocumentId]
            FOREIGN KEY (DecommissionDocumentId) REFERENCES dbo.Documents (Id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX IX_FixedAssets_InventoryNumber ON dbo.FixedAssets (InventoryNumber);
    CREATE NONCLUSTERED INDEX IX_FixedAssets_BuildingId             ON dbo.FixedAssets (BuildingId);
    CREATE NONCLUSTERED INDEX IX_FixedAssets_RoomId                 ON dbo.FixedAssets (RoomId);
    CREATE NONCLUSTERED INDEX IX_FixedAssets_ResponsibleEmployeeId  ON dbo.FixedAssets (ResponsibleEmployeeId);
    CREATE NONCLUSTERED INDEX IX_FixedAssets_DecommissionDocumentId ON dbo.FixedAssets (DecommissionDocumentId);
    CREATE NONCLUSTERED INDEX IX_FixedAssets_Category               ON dbo.FixedAssets (Category);
    CREATE NONCLUSTERED INDEX IX_FixedAssets_Status                 ON dbo.FixedAssets (Status);
END
GO

/* ---------- 21. Phase 11 — Substitutions & TaskDelegations ---------------- */
IF OBJECT_ID(N'dbo.Substitutions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Substitutions
    (
        Id                    INT             IDENTITY(1, 1) NOT NULL,
        OriginalEmployeeId    INT             NOT NULL,
        SubstituteEmployeeId  INT             NOT NULL,
        [From]                DATETIME        NOT NULL,
        [To]                  DATETIME        NOT NULL,
        Scope                 INT             NOT NULL,
        Reason                NVARCHAR(512)   NULL,
        IsActive              BIT             NOT NULL CONSTRAINT DF_Substitutions_IsActive DEFAULT (1),
        CreatedById           INT             NOT NULL,
        CONSTRAINT PK_dbo_Substitutions PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.Substitutions_dbo.Employees_OriginalEmployeeId]
            FOREIGN KEY (OriginalEmployeeId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.Substitutions_dbo.Employees_SubstituteEmployeeId]
            FOREIGN KEY (SubstituteEmployeeId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_Substitutions_OriginalEmployeeId
        ON dbo.Substitutions (OriginalEmployeeId);
    CREATE NONCLUSTERED INDEX IX_Substitutions_SubstituteEmployeeId
        ON dbo.Substitutions (SubstituteEmployeeId);
    CREATE NONCLUSTERED INDEX IX_Substitutions_Active_From_To
        ON dbo.Substitutions (IsActive, [From], [To]);
END
GO

IF OBJECT_ID(N'dbo.TaskDelegations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskDelegations
    (
        Id              INT             IDENTITY(1, 1) NOT NULL,
        TaskId          INT             NOT NULL,
        FromEmployeeId  INT             NOT NULL,
        ToEmployeeId    INT             NOT NULL,
        DelegatedAt     DATETIME        NOT NULL,
        Comment         NVARCHAR(512)   NULL,
        CONSTRAINT PK_dbo_TaskDelegations PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.TaskDelegations_dbo.DocumentTasks_TaskId]
            FOREIGN KEY (TaskId) REFERENCES dbo.DocumentTasks (Id),
        CONSTRAINT [FK_dbo.TaskDelegations_dbo.Employees_FromEmployeeId]
            FOREIGN KEY (FromEmployeeId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.TaskDelegations_dbo.Employees_ToEmployeeId]
            FOREIGN KEY (ToEmployeeId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_TaskDelegations_TaskId         ON dbo.TaskDelegations (TaskId);
    CREATE NONCLUSTERED INDEX IX_TaskDelegations_FromEmployeeId ON dbo.TaskDelegations (FromEmployeeId);
    CREATE NONCLUSTERED INDEX IX_TaskDelegations_ToEmployeeId   ON dbo.TaskDelegations (ToEmployeeId);
END
GO

/* ---------- 22. Phase 9 — Notifications ----------------------------------- */
IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notifications
    (
        Id                  INT             IDENTITY(1, 1) NOT NULL,
        RecipientId         INT             NOT NULL,
        Kind                INT             NOT NULL,
        Title               NVARCHAR(512)   NULL,
        Body                NVARCHAR(2048)  NULL,
        RelatedDocumentId   INT             NULL,
        RelatedTaskId       INT             NULL,
        RelatedApprovalId   INT             NULL,
        CreatedAt           DATETIME        NOT NULL,
        ReadAt              DATETIME        NULL,
        Channel             INT             NOT NULL CONSTRAINT DF_Notifications_Channel DEFAULT (0),
        SentToEmailAt       DATETIME        NULL,
        CONSTRAINT PK_dbo_Notifications PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.Notifications_dbo.Employees_RecipientId]
            FOREIGN KEY (RecipientId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_Notifications_RecipientId
        ON dbo.Notifications (RecipientId);
    CREATE NONCLUSTERED INDEX IX_Notifications_Recipient_Unread
        ON dbo.Notifications (RecipientId, ReadAt);
    CREATE NONCLUSTERED INDEX IX_Notifications_RelatedTask_Kind
        ON dbo.Notifications (RelatedTaskId, Kind);
END
GO

IF OBJECT_ID(N'dbo.NotificationPreferences', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationPreferences
    (
        Id              INT             IDENTITY(1, 1) NOT NULL,
        EmployeeId      INT             NOT NULL,
        Kind            INT             NOT NULL,
        Channel         INT             NOT NULL,
        IsEnabled       BIT             NOT NULL CONSTRAINT DF_NotificationPreferences_IsEnabled DEFAULT (1),
        EmailOverride   NVARCHAR(256)   NULL,
        CONSTRAINT PK_dbo_NotificationPreferences PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.NotificationPreferences_dbo.Employees_EmployeeId]
            FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX IX_NotificationPreferences_Employee_Kind
        ON dbo.NotificationPreferences (EmployeeId, Kind);
END
GO

/* ---------- 23. Phase 10 — Search index & saved searches ------------------ */
IF OBJECT_ID(N'dbo.AttachmentTextIndices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AttachmentTextIndices
    (
        Id                 INT             IDENTITY(1, 1) NOT NULL,
        AttachmentId       INT             NOT NULL,
        DocumentId         INT             NOT NULL,
        ExtractedText      NVARCHAR(MAX)   NULL,
        IndexedAt          DATETIME        NOT NULL,
        SourceContentHash  NVARCHAR(64)    NULL,
        CONSTRAINT PK_dbo_AttachmentTextIndices PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.AttachmentTextIndices_dbo.DocumentAttachments_AttachmentId]
            FOREIGN KEY (AttachmentId) REFERENCES dbo.DocumentAttachments (Id) ON DELETE CASCADE
    );
    CREATE UNIQUE NONCLUSTERED INDEX IX_AttachmentTextIndices_Attachment
        ON dbo.AttachmentTextIndices (AttachmentId);
    CREATE NONCLUSTERED INDEX IX_AttachmentTextIndices_Document
        ON dbo.AttachmentTextIndices (DocumentId);
END
GO

IF OBJECT_ID(N'dbo.SavedSearches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SavedSearches
    (
        Id          INT             IDENTITY(1, 1) NOT NULL,
        OwnerId     INT             NOT NULL,
        Name        NVARCHAR(128)   NOT NULL,
        FilterJson  NVARCHAR(MAX)   NULL,
        IsShared    BIT             NOT NULL CONSTRAINT DF_SavedSearches_IsShared DEFAULT (0),
        CreatedAt   DATETIME        NOT NULL,
        CONSTRAINT PK_dbo_SavedSearches PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.SavedSearches_dbo.Employees_OwnerId]
            FOREIGN KEY (OwnerId) REFERENCES dbo.Employees (Id)
    );
    CREATE NONCLUSTERED INDEX IX_SavedSearches_Owner ON dbo.SavedSearches (OwnerId);
END
GO

/* Опциональный полнотекстовый каталог + индекс для AttachmentTextIndices.
   На LocalDB / SQL Server Express без FT-сервиса блок завершится молча. */
BEGIN TRY
    IF SERVERPROPERTY('IsFullTextInstalled') = 1
        AND NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'AhuErpFt')
    BEGIN
        EXEC('CREATE FULLTEXT CATALOG AhuErpFt');
        EXEC('CREATE FULLTEXT INDEX ON dbo.AttachmentTextIndices(ExtractedText)
              KEY INDEX [PK_dbo_AttachmentTextIndices] ON AhuErpFt');
    END
END TRY
BEGIN CATCH
    /* Full-text каталог недоступен — поиск работает через LIKE/in-process. */
END CATCH
GO

/* ---------- 24. Phase 19 — Archive retention & destruction acts ----------- */
/* DestructionActs — акты о выделении к уничтожению архивных документов
   (Приказ Минкультуры от 31.03.2015 № 526, приложение № 21;
   Приказ Росархива от 20.12.2019 № 236). Жизненный цикл независим
   от DocumentStatus: Draft (0) → Approved (1) → Executed (2) | Cancelled (3). */
IF OBJECT_ID(N'dbo.DestructionActs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DestructionActs
    (
        Id                      INT             IDENTITY(1, 1) NOT NULL,
        ActNumber               NVARCHAR(64)    NOT NULL,
        ActDate                 DATETIME        NOT NULL,
        Status                  INT             NOT NULL CONSTRAINT DF_DestructionActs_Status DEFAULT (0),
        DraftedByEmployeeId     INT             NOT NULL,
        ApprovedByEmployeeId    INT             NULL,
        ApprovedAt              DATETIME        NULL,
        ExecutedAt              DATETIME        NULL,
        DestructionMethod       NVARCHAR(256)   NULL,
        Notes                   NVARCHAR(4096)  NULL,
        CONSTRAINT PK_dbo_DestructionActs PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.DestructionActs_dbo.Employees_DraftedByEmployeeId]
            FOREIGN KEY (DraftedByEmployeeId) REFERENCES dbo.Employees (Id),
        CONSTRAINT [FK_dbo.DestructionActs_dbo.Employees_ApprovedByEmployeeId]
            FOREIGN KEY (ApprovedByEmployeeId) REFERENCES dbo.Employees (Id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX IX_DestructionActs_ActNumber            ON dbo.DestructionActs (ActNumber);
    CREATE NONCLUSTERED INDEX        IX_DestructionActs_Status               ON dbo.DestructionActs (Status);
    CREATE NONCLUSTERED INDEX        IX_DestructionActs_ActDate              ON dbo.DestructionActs (ActDate DESC);
    CREATE NONCLUSTERED INDEX        IX_DestructionActs_DraftedByEmployeeId  ON dbo.DestructionActs (DraftedByEmployeeId);
    CREATE NONCLUSTERED INDEX        IX_DestructionActs_ApprovedByEmployeeId ON dbo.DestructionActs (ApprovedByEmployeeId);
END
GO

/* DestructionActItems — позиции акта (денормализованный снимок дел).
   При удалении исходного NomenclatureCases-дела позиция сохраняется. */
IF OBJECT_ID(N'dbo.DestructionActItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DestructionActItems
    (
        Id                      INT             IDENTITY(1, 1) NOT NULL,
        DestructionActId        INT             NOT NULL,
        NomenclatureCaseId      INT             NULL,
        CaseIndex               NVARCHAR(32)    NOT NULL,
        CaseTitle               NVARCHAR(512)   NOT NULL,
        CaseYear                INT             NOT NULL CONSTRAINT DF_DestructionActItems_CaseYear        DEFAULT (0),
        RetentionYears          INT             NOT NULL CONSTRAINT DF_DestructionActItems_RetentionYears  DEFAULT (0),
        DocumentCount           INT             NOT NULL CONSTRAINT DF_DestructionActItems_DocumentCount   DEFAULT (0),
        Article                 NVARCHAR(64)    NULL,
        Notes                   NVARCHAR(1024)  NULL,
        CONSTRAINT PK_dbo_DestructionActItems PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT [FK_dbo.DestructionActItems_dbo.DestructionActs_DestructionActId]
            FOREIGN KEY (DestructionActId) REFERENCES dbo.DestructionActs (Id) ON DELETE CASCADE,
        CONSTRAINT [FK_dbo.DestructionActItems_dbo.NomenclatureCases_NomenclatureCaseId]
            FOREIGN KEY (NomenclatureCaseId) REFERENCES dbo.NomenclatureCases (Id)
    );
    CREATE NONCLUSTERED INDEX IX_DestructionActItems_DestructionActId   ON dbo.DestructionActItems (DestructionActId);
    CREATE NONCLUSTERED INDEX IX_DestructionActItems_NomenclatureCaseId ON dbo.DestructionActItems (NomenclatureCaseId);
END
GO

PRINT N'AhuErpDb: схема создана / актуальна.';
PRINT N'Дальше можно накатить демо-данные: scripts/seed-db.sql';
GO
