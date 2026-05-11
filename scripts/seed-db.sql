/* ============================================================================
 * AhuErp — наполнение демо-БД «живой» рабочей средой.
 *
 * Назначение:
 *   После накатки `scripts/create-db.sql` (схема, актуальная по миграциям
 *   Phase 1–18) выполнить этот скрипт, чтобы в WPF-приложении было
 *   ощущение, что в учреждении кипит работа: несколько отделов с
 *   руководителями, активные задачи (в т.ч. с горящим сроком и
 *   просроченные), документы в разных статусах, переписка по
 *   входящим/исходящим, ИТО-тикеты с диагностикой и ВКС, движение ТМЦ,
 *   путевые листы с учётом ГСМ, подписи ПЭП/КЭП (включая один
 *   заблокированный КЭП документ), уведомления (часть прочитана, часть
 *   нет), активное замещение, журналы инструктажей и инвентаризаций,
 *   полнотекстовый индекс с реальным содержимым нескольких вложений,
 *   сохранённые поиски и непрерывная цепочка аудит-журнала, а также
 *   реестр зданий/помещений/основных средств и заявки на эксплуатацию.
 *
 * Запуск:
 *   1. SSMS / Azure Data Studio → подключиться к серверу с БД AhuErpDb.
 *   2. File → Open → seed-db.sql → F5.
 *   3. Скрипт идемпотентен: повторный запуск увидит ключевые строки и
 *      завершится `RAISERROR(N'Already seeded', 0, 1) WITH NOWAIT`.
 *
 * Учётные записи (пароль везде `password`, PBKDF2-SHA256, 100k итераций):
 *   admin / sterlikov / dorofeev / burdina / zaychenko / volkov / petrova
 *
 * Хэш «password» зафиксирован в проекте (см. Pbkdf2PasswordHasher.Hash).
 * Если формат хэша изменится — пересгенерируйте константу @pwHash.
 *
 * Примечания:
 *   - Файлы вложений физически НЕ создаются (нужны только метаданные в БД +
 *     заранее извлечённый текст в AttachmentTextIndices).
 *   - Hash-цепочка AuditLogs (Hash / PreviousHash) пересчитывается
 *     приложением (`IAuditService.Append`) при следующей реальной
 *     операции; в seed обе колонки оставлены NULL.
 *   - Скрипт намеренно выполняется одной T-SQL пачкой (без GO) —
 *     это позволяет использовать @pwHash и сделать ранний RETURN при
 *     повторном запуске; SET IDENTITY_INSERT включается/выключается
 *     по одной таблице за раз.
 * ========================================================================== */

USE [AhuErpDb];
GO

SET NOCOUNT ON;
GO

/* ---------- 0. Идемпотентность: уже посеяно — выходим --------------------- */
IF EXISTS (SELECT 1 FROM dbo.Employees WHERE FullName LIKE N'%Стерликов%')
BEGIN
    RAISERROR(N'AhuErp: demo seed already applied — пропускаем.', 0, 1) WITH NOWAIT;
    RETURN;
END;

DECLARE @pwHash NVARCHAR(512) = N'100000.AQIDBAUGBwgJCgsMDQ4PEA==.7cyBZDaG9OlWsUaYsNJGCHei/cERxR/FFPfRr1R4A9M=';

/* ============================================================================
 * 1. ОТДЕЛЫ (Phase 11) — иерархия и руководители заполнятся ниже после
 *    вставки сотрудников. Здесь только создаём строки.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Departments ON;
INSERT INTO dbo.Departments (Id, Name, ShortCode, IsActive, ParentDepartmentId, HeadEmployeeId) VALUES
    (1, N'МКУ АХУ Балашовского муниципального района',           N'АХУ',   1, NULL, NULL),
    (2, N'Администрация',                                         N'АДМ',   1, NULL, NULL),
    (3, N'Канцелярия',                                            N'КАН',   1, NULL, NULL),
    (4, N'Служба информационно-технического обеспечения',         N'СИТО',  1, NULL, NULL),
    (5, N'Архивный отдел',                                        N'АРХ',   1, NULL, NULL),
    (6, N'Склад и ТМЦ',                                           N'СКЛАД', 1, NULL, NULL),
    (7, N'Транспортная служба',                                   N'ТР',    1, NULL, NULL);
SET IDENTITY_INSERT dbo.Departments OFF;

/* ============================================================================
 * 2. СОТРУДНИКИ
 *    Role: Admin=0, Manager=1, Archivist=2, TechSupport=3, WarehouseManager=4
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Employees ON;
INSERT INTO dbo.Employees (Id, FullName, [Position], [Role], PasswordHash, Email, DepartmentId, IsActive, TerminatedAt, LastPasswordChangeAt, LockedUntil) VALUES
    (1,  N'Администратор информационной системы',  N'Системный администратор АИС «АХУ»', 0, @pwHash, N'admin@ahu.local',     2, 1, NULL,                          DATEADD(DAY, -30, GETDATE()), NULL),
    (2,  N'Иванова Ольга Викторовна',               N'Заместитель директора',             1, @pwHash, N'ivanova@ahu.local',   2, 1, NULL,                          DATEADD(DAY, -45, GETDATE()), NULL),
    (3,  N'Петрова Анна Сергеевна',                 N'Делопроизводитель',                 1, @pwHash, N'petrova@ahu.local',   3, 1, NULL,                          DATEADD(DAY, -10, GETDATE()), NULL),
    (4,  N'Стерликов Дмитрий Николаевич',           N'Руководитель службы по ИТО',        1, @pwHash, N'sterlikov@ahu.local', 4, 1, NULL,                          DATEADD(DAY, -60, GETDATE()), NULL),
    (5,  N'Дорофеев Артём Валерьевич',              N'Специалист по компьютерным сетям',  3, @pwHash, N'dorofeev@ahu.local',  4, 1, NULL,                          DATEADD(DAY, -20, GETDATE()), NULL),
    (6,  N'Королёв Никита Александрович',           N'Инженер 1 категории',               3, @pwHash, N'korolev@ahu.local',   4, 1, NULL,                          DATEADD(DAY, -15, GETDATE()), NULL),
    (7,  N'Бурдина Галина Николаевна',              N'Начальник архивного отдела',        2, @pwHash, N'burdina@ahu.local',   5, 1, NULL,                          DATEADD(DAY, -25, GETDATE()), NULL),
    (8,  N'Сёмина Елена Владимировна',              N'Архивист',                          2, @pwHash, N'semina@ahu.local',    5, 1, NULL,                          DATEADD(DAY, -12, GETDATE()), NULL),
    (9,  N'Зайченко Татьяна Александровна',         N'Заведующая складом',                4, @pwHash, N'zaychenko@ahu.local', 6, 1, NULL,                          DATEADD(DAY, -35, GETDATE()), NULL),
    (10, N'Волков Сергей Игоревич',                 N'Водитель',                          4, @pwHash, N'volkov@ahu.local',    7, 1, NULL,                          DATEADD(DAY, -18, GETDATE()), NULL),
    (11, N'Сидоров Павел Иванович (уволен)',        N'Бывший делопроизводитель',          1, NULL,    N'sidorov@ahu.local',   3, 0, DATEADD(MONTH, -3, GETDATE()), NULL,                          NULL);
SET IDENTITY_INSERT dbo.Employees OFF;

/* ---------- 2a. Отделы → руководители и иерархия (Phase 11) --------------- */
UPDATE dbo.Departments SET ParentDepartmentId = NULL, HeadEmployeeId = 1  WHERE Id = 1;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 2  WHERE Id = 2;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 3  WHERE Id = 3;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 4  WHERE Id = 4;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 7  WHERE Id = 5;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 9  WHERE Id = 6;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 10 WHERE Id = 7;

/* ============================================================================
 * 3. СПРАВОЧНИКИ ДОКУМЕНТООБОРОТА (Phase 7)
 * ========================================================================== */
SET IDENTITY_INSERT dbo.DocumentTypeRefs ON;
INSERT INTO dbo.DocumentTypeRefs (Id, Name, ShortCode, DefaultDirection, DefaultRetentionYears, RegistrationNumberTemplate, IsActive) VALUES
    (1, N'Письмо входящее',     N'ВХ',  1, 5,  N'ВХ-{YYYY}-{NNNNN}',  1),
    (2, N'Письмо исходящее',    N'ИСХ', 2, 5,  N'ИСХ-{YYYY}-{NNNNN}', 1),
    (3, N'Служебная записка',   N'СЗ',  0, 3,  N'СЗ-{YYYY}-{NNNNN}',  1),
    (4, N'Приказ',              N'ПРК', 0, 75, N'ПРК-{YYYY}-{NNN}',   1),
    (5, N'Распоряжение',        N'РСП', 0, 5,  N'РСП-{YYYY}-{NNN}',   1),
    (6, N'Договор',              N'ДОГ', 0, 75, N'ДОГ-{YYYY}-{NNN}',   1),
    (7, N'Заявка ИТО',          N'ИТО', 0, 3,  N'ИТО-{YYYY}-{NNNN}',  1),
    (8, N'Архивный запрос',     N'АЗ',  1, 5,  N'АЗ-{YYYY}-{NNNN}',   1);
SET IDENTITY_INSERT dbo.DocumentTypeRefs OFF;

SET IDENTITY_INSERT dbo.NomenclatureCases ON;
INSERT INTO dbo.NomenclatureCases (Id, [Index], Title, DepartmentId, RetentionPeriodYears, Article, [Year], IsActive) VALUES
    (1, N'01-01', N'Приказы по основной деятельности',             2, 75, N'19а', YEAR(GETDATE()), 1),
    (2, N'02-01', N'Переписка с органами местного самоуправления', 3, 5,  N'33',  YEAR(GETDATE()), 1),
    (3, N'02-02', N'Служебные записки',                            3, 3,  N'88',  YEAR(GETDATE()), 1),
    (4, N'03-01', N'Заявки и тикеты службы ИТО',                   4, 3,  N'255', YEAR(GETDATE()), 1),
    (5, N'04-01', N'Договоры на поставку канцелярских товаров',    6, 5,  N'436', YEAR(GETDATE()), 1),
    (6, N'04-02', N'Путевые листы',                                7, 5,  N'553', YEAR(GETDATE()), 1),
    (7, N'05-01', N'Архивные запросы граждан',                     5, 5,  N'166', YEAR(GETDATE()), 1);
SET IDENTITY_INSERT dbo.NomenclatureCases OFF;

/* ============================================================================
 * 3a. NomenclatureCounters (Phase 15) — счётчики порядковых номеров.
 * ========================================================================== */
INSERT INTO dbo.NomenclatureCounters (TypeCode, [Year], LastNumber) VALUES
    (N'ВХ',  YEAR(GETDATE()), 37),
    (N'ИСХ', YEAR(GETDATE()), 21),
    (N'СЗ',  YEAR(GETDATE()), 20),
    (N'ПРК', YEAR(GETDATE()),  7),
    (N'РСП', YEAR(GETDATE()),  9),
    (N'ДОГ', YEAR(GETDATE()),  4),
    (N'ИТО', YEAR(GETDATE()), 91),
    (N'АЗ',  YEAR(GETDATE()), 43);

/* ============================================================================
 * 4. ЗДАНИЯ И ПОМЕЩЕНИЯ (Phase 18)
 *    Building.Name уникален. Room.(BuildingId, Number) уникален.
 *    Room.Purpose: Office=1, Server=2, Storage=3, Archive=4, Warehouse=5, Garage=6.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Buildings ON;
INSERT INTO dbo.Buildings (Id, Name, Address, TotalAreaSqm, FloorCount, CommissionedYear, ResponsibleEmployeeId, Notes) VALUES
    (1, N'Главный корпус АХУ',              N'г. Балашов, ул. Советская, 174',     1850.50, 3, 1985, 1,  N'Капитальный ремонт фасада в 2020 г.'),
    (2, N'Архивный корпус',                  N'г. Балашов, ул. Советская, 174а',     620.00, 2, 1992, 7,  N'Климат-контроль в архивохранилищах.'),
    (3, N'Гараж транспортной службы',        N'г. Балашов, пр-кт Космонавтов, 12',   480.00, 1, 1998, 10, N'4 бокса + смотровая яма.');
SET IDENTITY_INSERT dbo.Buildings OFF;

SET IDENTITY_INSERT dbo.Rooms ON;
INSERT INTO dbo.Rooms (Id, BuildingId, Number, Floor, AreaSqm, Purpose, ResponsibleEmployeeId, Notes) VALUES
    (1,  1, N'101', 1, 24.50,  1, 1,  N'Приёмная директора'),
    (2,  1, N'201', 2, 18.20,  1, 2,  N'Кабинет заместителя директора'),
    (3,  1, N'207', 2, 32.40,  1, 3,  N'Канцелярия'),
    (4,  1, N'305', 3, 28.10,  1, 4,  N'Кабинет ИТО'),
    (5,  1, N'310', 3, 16.80,  2, 5,  N'Серверная'),
    (6,  1, N'401', 1, 64.00,  5, 9,  N'Складское помещение №1'),
    (7,  2, N'А-1', 1, 120.00, 4, 7,  N'Архивохранилище долговременного хранения'),
    (8,  2, N'А-2', 2, 80.00,  4, 8,  N'Архивохранилище 75-летнее'),
    (9,  3, N'Г-1', 1, 90.00,  6, 10, N'Бокс №1 (Lada Largus / Renault Logan)'),
    (10, 3, N'Г-2', 1, 90.00,  6, 10, N'Бокс №2 (ГАЗель / УАЗ)');
SET IDENTITY_INSERT dbo.Rooms OFF;

/* ============================================================================
 * 5. ТРАНСПОРТ (Phase 1 + Phase 15 + Phase 17)
 *    CurrentStatus: Available=0, OnTrip=1, Maintenance=2.
 *    FuelType: Petrol=0, Diesel=1, Gas=2, Electric=3.
 *    VehicleClass: Passenger=0, Cargo=1, MiniBus=2, SUV=3.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Vehicles ON;
INSERT INTO dbo.Vehicles (Id, Model, LicensePlate, CurrentStatus, FuelType, FuelConsumptionPer100Km,
                          VehicleClass, Make, [Year], Vin, OdometerCurrent, NextMaintenanceOdometer,
                          OsagoExpiry, TechInspectionExpiry) VALUES
    /* #2 и #4: статусы выровнены с фактическими путевыми листами (Trip #2 закрыт 5 дн. назад, Trip #4 активен). */
    (1, N'Largus универсал',  N'А123БВ 64', 0, 0,  9.50, 1, N'Lada',    2021, N'XTAFS035LM0934512',  78420,  85000, DATEADD(MONTH,  4, GETDATE()), DATEADD(MONTH, 5, GETDATE())),
    (2, N'NEXT грузовой',     N'В777ТТ 64', 0, 1, 14.00, 1, N'ГАЗ',      2019, N'X9633440K0046721', 142300, 150000, DATEADD(MONTH,  1, GETDATE()), DATEADD(MONTH, 2, GETDATE())),
    (3, N'Патриот',           N'Е111КХ 64', 2, 0, 13.20, 3, N'УАЗ',      2018, N'XTT316300J1015533', 98750, 100000, DATEADD(MONTH, -1, GETDATE()), DATEADD(MONTH, 3, GETDATE())),
    (4, N'Logan',             N'К234АА 64', 1, 0,  7.80, 0, N'Renault',  2022, N'X7L4SRDC2KH123456', 41200,  50000, DATEADD(MONTH,  6, GETDATE()), DATEADD(MONTH, 9, GETDATE()));
SET IDENTITY_INSERT dbo.Vehicles OFF;

/* ============================================================================
 * 6. СЕТЕВЫЕ СЕГМЕНТЫ И ОБОРУДОВАНИЕ ИТО (Phase 14)
 *    EquipmentType: Pc=0, Printer=1, Switch=2, AccessPoint=3, IpPhone=4,
 *                   IpCamera=5, Server=6, VideoConferenceUnit=7, Ups=8, Other=99.
 *    EquipmentStatus: Working=0, InRepair=1, SentToVendor=2, Decommissioned=3, InReserve=4.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.NetworkSegments ON;
INSERT INTO dbo.NetworkSegments (Id, [Name], Vlan, IpRange, SubnetMask, Gateway, Dns, Notes) VALUES
    (1, N'Серверный сегмент', N'10', N'192.168.10.0/24', N'255.255.255.0', N'192.168.10.1', N'192.168.10.10, 192.168.10.11', N'Контроллер домена, файловый сервер'),
    (2, N'Канцелярия',         N'20', N'192.168.20.0/24', N'255.255.255.0', N'192.168.20.1', N'192.168.10.10',                 N'Принтеры и АРМ канцелярии'),
    (3, N'Архив',              N'30', N'192.168.30.0/24', N'255.255.255.0', N'192.168.30.1', N'192.168.10.10',                 N'Изолированный, доступ только на чтение к файловому серверу'),
    (4, N'Гостевой Wi-Fi',     N'90', N'192.168.90.0/24', N'255.255.255.0', N'192.168.90.1', N'8.8.8.8',                       N'Без доступа во внутренние ресурсы');
SET IDENTITY_INSERT dbo.NetworkSegments OFF;

SET IDENTITY_INSERT dbo.Equipment ON;
INSERT INTO dbo.Equipment (Id, InventoryNumber, [Type], Model, SerialNumber, MacAddress, IpAddress, Room,
                           ResponsibleEmployeeId, InServiceDate, WarrantyExpiry, [Status], NetworkSegmentId, Notes) VALUES
    /* Исправлены коды EquipmentType / EquipmentStatus в соответствии с фактическими
       enum'ами в src/AhuErp.Core/Models/EquipmentType.cs и EquipmentStatus.cs.
       Сервер #1 помечен как SentToVendor=2 (парный с ItTicket #13.IsSentToVendor=1). */
    (1, N'ИТО-0001', 6, N'HP ProLiant DL380 Gen10', N'CZJ9230XYZ',  N'AA:BB:CC:DD:EE:01', N'192.168.10.10', N'310', 4, DATEADD(YEAR, -3, GETDATE()), DATEADD(YEAR,  1, GETDATE()), 2, 1,    N'Контроллер домена, файловый сервер (в сервисе по тикету ИТО-2026-00104)'),
    (2, N'ИТО-0014', 2, N'Cisco SG250-26',          N'FOC2336X1AB', N'AA:BB:CC:DD:EE:14', N'192.168.10.2',  N'310', 5, DATEADD(YEAR, -2, GETDATE()), DATEADD(YEAR,  2, GETDATE()), 0, 1,    N'Управляемый коммутатор серверной'),
    (3, N'ИТО-0027', 3, N'TP-Link EAP245',          N'2218A40023',  N'AA:BB:CC:DD:EE:27', N'192.168.20.20', N'207', 5, DATEADD(YEAR, -1, GETDATE()), DATEADD(MONTH, 4, GETDATE()), 1, 2,    N'Точка доступа Wi-Fi канцелярии (нестабильна с 2026-04)'),
    (4, N'ИТО-0033', 1, N'Canon LBP6030',           N'AABC012345',  NULL,                  NULL,             N'305', 5, DATEADD(YEAR, -2, GETDATE()), NULL,                          0, NULL, N'Принтер ИТО (требуется замена тонера)'),
    (5, N'ИТО-0034', 1, N'Canon LBP6030',           N'AABC012346',  NULL,                  NULL,             N'305', 5, DATEADD(YEAR, -2, GETDATE()), NULL,                          0, NULL, N'Принтер ИТО (требуется замена тонера)'),
    (6, N'ИТО-0078', 0, N'HP EliteDesk 800 G5',     N'2UA0245TR1',  N'AA:BB:CC:DD:EE:78', N'192.168.20.78', N'207', 3, DATEADD(YEAR, -1, GETDATE()), DATEADD(YEAR,  2, GETDATE()), 0, 2,    N'АРМ Петровой');
SET IDENTITY_INSERT dbo.Equipment OFF;

/* ============================================================================
 * 7. ТМЦ И СКЛАД (Unit / MinimumBalance — миграция AddInventoryItemUnitAndMinimumBalance)
 *    TotalQuantity = SUM(QuantityChanged) по InventoryTransactions ниже —
 *    иначе сервис InventoryService будет «лечить» расхождения в дашбордах.
 *    Category: Stationery=0, Hardware=1, Hygiene=2.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.InventoryItems ON;
INSERT INTO dbo.InventoryItems (Id, [Name], Category, TotalQuantity, Unit, MinimumBalance) VALUES
    (1, N'Бумага A4 «Снегурочка», 500 листов',   0,  87, N'пач.',  30),
    (2, N'Картридж Canon 725 (для LBP6030)',    1,  17, N'шт.',    8),
    (3, N'Тонер HP CF283A',                      1,   8, N'шт.',    4),
    (4, N'Ручка шариковая синяя BIC',            0, 250, N'шт.',  100),
    (5, N'Папка-регистратор A4 70мм',            0,  46, N'шт.',   20),
    (6, N'Жидкое мыло для рук, 5л',              2,   9, N'шт.',    3),
    (7, N'Перчатки одноразовые, упак. 100 шт.',  2,  22, N'упак.',  5);
SET IDENTITY_INSERT dbo.InventoryItems OFF;

/* ============================================================================
 * 8. ДОКУМЕНТЫ (TPH: Document / ItTicket / ArchiveRequest)
 *    Type:   General=0, Office=1, Archive=2, It=3, Fleet=4,
 *            Incoming=5, Internal=6, ArchiveRequest=7
 *    Direction: Internal=0, Incoming=1, Outgoing=2
 *    AccessLevel (Document): Public=0, Internal=1, Confidential=2
 *    Status: New=0, InProgress=1, OnHold=2, Completed=3, Cancelled=4
 *    ApprovalRouteStatus: Draft=0, InProgress=1, Completed=2, Rejected=3, Cancelled=4
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Documents ON;
INSERT INTO dbo.Documents (
    Id, [Type], Direction, AccessLevel, RegistrationNumber, RegistrationDate,
    DocumentTypeRefId, NomenclatureCaseId, AuthorId, Title, Summary, Correspondent,
    IncomingNumber, IncomingDate, CreationDate, Deadline, [Status], AssignedEmployeeId,
    BasisDocumentId, ApprovalStatus, HasPassportScan, HasWorkBookScan, ArchiveRequestKind,
    AffectedEquipment, ResolutionNotes,
    AffectedEquipmentId, Kind, IsSentToVendor, VendorName, VendorTicketNumber,
    VendorReturnDeadline, CompletedAt,
    IsLocked, CurrentVersionAttachmentId,
    DocumentDiscriminator
) VALUES
    /* 1. Закрытое исходящее письмо (3 недели назад) */
    (1, 1, 2, 0, N'ИСХ-2026-00012', DATEADD(DAY, -22, GETDATE()),
     2, 2, 3, N'Ответ на запрос Министерства финансов о бюджете 2026', N'Подготовлены пояснения по статье расходов 02-04. Подписано и отправлено почтой РФ.', N'Министерство финансов СО',
     NULL, NULL, DATEADD(DAY, -25, GETDATE()), DATEADD(DAY, -20, GETDATE()), 3, 2,
     NULL, 2, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     0, NULL, N'Document'),

    /* 2. Входящее письмо (на исполнении, срок завтра) */
    (2, 1, 1, 0, N'ВХ-2026-00037', DATEADD(DAY, -5, GETDATE()),
     1, 2, 3, N'О предоставлении сведений о работе архивного отдела за 1 квартал', N'Запрашиваются количественные показатели обработки запросов.', N'Управление по делам архивов СО',
     N'04-12/345', DATEADD(DAY, -7, GETDATE()), DATEADD(DAY, -5, GETDATE()), DATEADD(DAY, 1, GETDATE()), 1, 7,
     NULL, 0, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     0, NULL, N'Document'),

    /* 3. Внутренняя СЗ — на согласовании */
    (3, 6, 0, 1, N'СЗ-2026-00018', DATEADD(DAY, -3, GETDATE()),
     3, 3, 5, N'О замене картриджей в принтерах Canon LBP6030 кабинета 305', N'Прошу заменить 4 тонер-картриджа Canon 725 в принтерах Canon LBP6030 (3 шт., каб. 305) для бесперебойной работы.', NULL,
     NULL, NULL, DATEADD(DAY, -3, GETDATE()), DATEADD(DAY, 2, GETDATE()), 1, 9,
     NULL, 1, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     0, NULL, N'Document'),

    /* 4. Приказ — подписан КЭП, заблокирован */
    (4, 1, 0, 1, N'ПРК-2026-00007', DATEADD(DAY, -10, GETDATE()),
     4, 1, 1, N'Об утверждении графика отпусков на 2026 год', N'Утверждается график очередных отпусков сотрудников учреждения на 2026 год согласно приложению. К исполнению.', NULL,
     NULL, NULL, DATEADD(DAY, -12, GETDATE()), DATEADD(DAY, -8, GETDATE()), 3, 1,
     NULL, 2, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     1, NULL, N'Document'),

    /* 5. Тикет ИТО (просрочен) — связан с Equipment.Id=3 (TP-Link EAP245) */
    (5, 3, 0, 0, N'ИТО-2026-00091', DATEADD(DAY, -8, GETDATE()),
     7, 4, 3, N'Не работает Wi-Fi в кабинете 207', N'Сотрудники жалуются на нестабильную работу Wi-Fi после выходных.', NULL,
     NULL, NULL, DATEADD(DAY, -8, GETDATE()), DATEADD(DAY, -2, GETDATE()), 1, 5,
     NULL, 0, NULL, NULL, NULL, N'Точка доступа TP-Link EAP245 (каб. 207)', NULL,
     3, 1, 0, NULL, NULL, NULL, NULL,
     0, NULL, N'ItTicket'),

    /* 6. Заявка на ТМЦ — закрыта */
    (6, 1, 0, 0, N'СЗ-2026-00015', DATEADD(DAY, -14, GETDATE()),
     3, 3, 5, N'О выдаче бумаги формата A4 на отдел ИТО (10 пачек)', N'Бумага требуется для печати квартальной отчётности. Выдано полностью.', NULL,
     NULL, NULL, DATEADD(DAY, -16, GETDATE()), DATEADD(DAY, -10, GETDATE()), 3, 5,
     NULL, 2, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     0, NULL, N'Document'),

    /* 7. Архивный запрос — на исполнении, срок < 24ч */
    (7, 7, 1, 0, N'АЗ-2026-00043', DATEADD(DAY, -6, GETDATE()),
     8, 7, 7, N'Запрос Иванова И.И. о подтверждении трудового стажа за 1995-1998 гг.', N'Гражданин просит выдать архивную справку о работе на муниципальном предприятии.', N'Иванов Иван Иванович',
     NULL, NULL, DATEADD(DAY, -6, GETDATE()), DATEADD(HOUR, 18, GETDATE()), 1, 8,
     NULL, 0, 0, 0, 1, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     0, NULL, N'ArchiveRequest'),

    /* 8. Договор — подписан ПЭП, не заблокирован */
    (8, 1, 0, 2, N'ДОГ-2026-00004', DATEADD(DAY, -2, GETDATE()),
     6, 5, 9, N'Договор поставки канцелярских товаров № 04-2026', N'Договор поставки бумаги, ручек, маркеров и прочих расходных материалов на 2 квартал 2026 г.', N'ООО «Канцоптторг»',
     NULL, NULL, DATEADD(DAY, -4, GETDATE()), DATEADD(DAY, 5, GETDATE()), 1, 9,
     NULL, 1, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     0, NULL, N'Document'),

    /* 9. Черновик СЗ — Дорофеев пишет, ещё не зарегистрировано */
    (9, 6, 0, 0, NULL, NULL,
     3, 3, 5, N'О необходимости приобретения дополнительного коммутатора', N'Текущий коммутатор Cisco SG250-26 не справляется с нагрузкой в часы пик.', NULL,
     NULL, NULL, DATEADD(DAY, -1, GETDATE()), DATEADD(DAY, 7, GETDATE()), 0, 4,
     NULL, 0, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     0, NULL, N'Document'),

    /* 10. Письмо — ответ на №2 (basis) */
    (10, 1, 2, 0, N'ИСХ-2026-00021', DATEADD(DAY, -1, GETDATE()),
     2, 2, 7, N'Ответ на запрос ВХ-2026-00037 (квартальный отчёт)', N'Подготовлена сводка по обработанным запросам граждан за 1 квартал 2026 года.', N'Управление по делам архивов СО',
     NULL, NULL, DATEADD(DAY, -2, GETDATE()), DATEADD(DAY, -1, GETDATE()), 3, 7,
     2, 2, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     0, NULL, N'Document'),

    /* 11. Распоряжение — на согласовании */
    (11, 1, 0, 1, N'РСП-2026-00009', DATEADD(DAY, -1, GETDATE()),
     5, 1, 2, N'О проведении инвентаризации ТМЦ во 2 квартале', N'Назначить комиссию для проведения инвентаризации.', NULL,
     NULL, NULL, DATEADD(DAY, -2, GETDATE()), DATEADD(DAY, 14, GETDATE()), 1, 9,
     NULL, 1, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     0, NULL, N'Document'),

    /* 12. Внутренний документ Fleet — путевой лист */
    (12, 4, 0, 0, N'СЗ-2026-00020', DATEADD(DAY, -1, GETDATE()),
     3, 6, 10, N'Заявка на выезд автомобиля Lada Largus', N'Доставка корреспонденции в районную администрацию.', NULL,
     NULL, NULL, DATEADD(DAY, -1, GETDATE()), DATEADD(DAY, 0, GETDATE()), 3, 10,
     NULL, 2, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     0, NULL, N'Document'),

    /* 13. Тикет ИТО — отправлено вендору (Phase 14, IsSentToVendor=1) */
    (13, 3, 0, 0, N'ИТО-2026-00104', DATEADD(DAY, -3, GETDATE()),
     7, 4, 5, N'Сервер HP DL380 — гул вентилятора, требуется диагностика', N'После последнего ребута слышен повышенный шум — есть подозрение на неисправность вентилятора блока питания.', NULL,
     NULL, NULL, DATEADD(DAY, -3, GETDATE()), DATEADD(DAY, 10, GETDATE()), 1, 5,
     NULL, 0, NULL, NULL, NULL, N'Сервер HP ProLiant DL380 Gen10 (серверная)', NULL,
     1, 1, 1, N'ООО «Сервис-ИТ Балашов»', N'SVC-2026-0445', DATEADD(DAY, 7, GETDATE()), NULL,
     0, NULL, N'ItTicket'),

    /* 14. Акт инвентаризации — внутренний документ-результат */
    (14, 6, 0, 1, N'АКТ-2026-00002', DATEADD(DAY, -55, GETDATE()),
     3, 1, 2, N'Акт инвентаризации ТМЦ за 1 квартал 2026 года', N'Оформлено по итогам инвентаризации, выявлены небольшие расхождения по бумаге А4. Подписано всеми членами комиссии.', NULL,
     NULL, NULL, DATEADD(DAY, -56, GETDATE()), DATEADD(DAY, -50, GETDATE()), 3, 2,
     NULL, 2, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL,
     0, NULL, N'Document');
SET IDENTITY_INSERT dbo.Documents OFF;

/* ============================================================================
 * 9. ВЛОЖЕНИЯ (Phase 7) — несколько с реальным текстом для индексирования.
 *    StoragePath — относительный путь, сами файлы physically могут отсутствовать.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.DocumentAttachments ON;
INSERT INTO dbo.DocumentAttachments (Id, DocumentId, AttachmentGroupId, FileName, StoragePath, VersionNumber, IsCurrentVersion, UploadedAt, UploadedById, Comment, Hash, FileType, SizeBytes) VALUES
    (1, 1,  1, N'ISH-2026-00012.docx',          N'demo-storage/ISH-2026-00012/v1_ISH-2026-00012.docx',           1, 1, DATEADD(DAY, -22, GETDATE()), 3, NULL,                                  N'h-c1a1',  0,  18432),
    (2, 2,  2, N'VH-2026-00037-skan.pdf',       N'demo-storage/VH-2026-00037/v1_VH-2026-00037-skan.pdf',         1, 1, DATEADD(DAY,  -5, GETDATE()), 3, NULL,                                  N'h-c2a2',  1, 220300),
    (3, 3,  3, N'SZ-cartridge.docx',            N'demo-storage/SZ-2026-00018/v1_SZ-cartridge.docx',              1, 1, DATEADD(DAY,  -3, GETDATE()), 5, NULL,                                  N'h-c3a3',  0,  12200),
    (4, 4,  4, N'PRK-otpuska-2026.docx',        N'demo-storage/PRK-2026-00007/v1_PRK-otpuska-2026.docx',         1, 1, DATEADD(DAY, -10, GETDATE()), 1, NULL,                                  N'h-c4a4',  0,  28100),
    (5, 4,  5, N'PRK-otpuska-2026.docx.sig',    N'demo-storage/PRK-2026-00007/v1_PRK-otpuska-2026.docx.sig',     1, 1, DATEADD(DAY,  -8, GETDATE()), 1, N'Открепленная КЭП',                   N'h-c5a5',  2,   7700),
    (6, 6,  6, N'SZ-bumaga.txt',                N'demo-storage/SZ-2026-00015/v1_SZ-bumaga.txt',                  1, 1, DATEADD(DAY, -16, GETDATE()), 5, NULL,                                  N'h-c6a6',  0,    480),
    (7, 8,  7, N'DOG-2026-00004-poso.docx',     N'demo-storage/DOG-2026-00004/v1_DOG-2026-00004-poso.docx',      1, 0, DATEADD(DAY,  -4, GETDATE()), 9, NULL,                                  N'h-c7a7',  0,  84200),
    (8, 8,  7, N'DOG-2026-00004-poso.docx',     N'demo-storage/DOG-2026-00004/v2_DOG-2026-00004-poso.docx',      2, 1, DATEADD(DAY,  -2, GETDATE()), 9, N'Версия после правок юриста',         N'h-c8a8',  0,  86400),
    (9, 10, 8, N'ISH-2026-00021.docx',          N'demo-storage/ISH-2026-00021/v1_ISH-2026-00021.docx',           1, 1, DATEADD(DAY,  -2, GETDATE()), 7, NULL,                                  N'h-c9a9',  0,  16700),
    (10, 14, 9, N'AKT-inventarizatsii-Q1.pdf',  N'demo-storage/AKT-2026-00002/v1_AKT-inventarizatsii-Q1.pdf',    1, 1, DATEADD(DAY, -50, GETDATE()), 2, N'Подписано комиссией',                N'h-c10a10', 1, 192800);
SET IDENTITY_INSERT dbo.DocumentAttachments OFF;

/* Текущая версия для документов с несколькими версиями + lock на КЭП-документе */
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 1  WHERE Id = 1;
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 2  WHERE Id = 2;
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 3  WHERE Id = 3;
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 4  WHERE Id = 4;
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 6  WHERE Id = 6;
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 8  WHERE Id = 8;   -- v2 договора
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 9  WHERE Id = 10;
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 10 WHERE Id = 14;

/* ============================================================================
 * 10. РЕЗОЛЮЦИИ И ЗАДАЧИ (Phase 7)
 *    DocumentTaskStatus: New=0, InProgress=1, OnReview=2, Completed=3,
 *                        Cancelled=4, Overdue=5.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.DocumentResolutions ON;
INSERT INTO dbo.DocumentResolutions (Id, DocumentId, AuthorId, Text, IssuedAt) VALUES
    (1, 2, 1, N'Бурдиной Г.Н. — подготовить ответ в срок до 28.04. Контролирует Иванова О.В.',                  DATEADD(DAY, -5, GETDATE())),
    (2, 3, 4, N'Зайченко Т.А. — выдать со склада 4 картриджа Canon 725. Дорофееву А.В. — установить.',          DATEADD(DAY, -3, GETDATE())),
    (3, 7, 7, N'Сёминой Е.В. — поднять архивные дела БМР-1995-Л за 1995-1998 и подготовить справку.',           DATEADD(DAY, -6, GETDATE()));
SET IDENTITY_INSERT dbo.DocumentResolutions OFF;

SET IDENTITY_INSERT dbo.DocumentTasks ON;
INSERT INTO dbo.DocumentTasks (Id, DocumentId, ResolutionId, ParentTaskId, AuthorId, ExecutorId, ControllerId, CoExecutors, Description, CreatedAt, Deadline, [Status], CompletedAt, ReportText, IsCritical) VALUES
    /* Просроченная задача */
    (1, 5, NULL, NULL, 1, 5, 4, NULL, N'Восстановить работу Wi-Fi в кабинете 207, проверить точку доступа.',
     DATEADD(DAY, -8, GETDATE()), DATEADD(DAY, -2, GETDATE()), 5, NULL, NULL, 1),
    /* Горящий срок < 24ч */
    (2, 7, 3, NULL, 7, 8, 7, NULL, N'Поднять архивные дела за 1995-1998 и подготовить справку для гражданина Иванова И.И.',
     DATEADD(DAY, -6, GETDATE()), DATEADD(HOUR, 18, GETDATE()), 1, NULL, NULL, 0),
    /* Назначена Стерликову (см. замещение) */
    (3, 3, 2, NULL, 4, 4, 1, NULL, N'Принять служебную записку, назначить исполнителя на склад/ИТО.',
     DATEADD(DAY, -3, GETDATE()), DATEADD(DAY, 2, GETDATE()), 1, NULL, NULL, 0),
    /* Закрытая */
    (4, 6, NULL, NULL, 5, 9, 4, NULL, N'Выдать со склада 10 пачек бумаги A4.',
     DATEADD(DAY, -16, GETDATE()), DATEADD(DAY, -10, GETDATE()), 3, DATEADD(DAY, -11, GETDATE()), N'Выдано полностью, расписка в журнале.', 0),
    /* Закрытая */
    (5, 1, NULL, NULL, 3, 3, 1, NULL, N'Подготовить и направить ответ в Минфин.',
     DATEADD(DAY, -25, GETDATE()), DATEADD(DAY, -22, GETDATE()), 3, DATEADD(DAY, -22, GETDATE()), N'Ответ направлен почтой РФ, трек 80012345.', 1),
    /* Подзадача под задачу 3 */
    (6, 3, 2, 3, 4, 9, 4, NULL, N'Выдать со склада 4 картриджа Canon 725.',
     DATEADD(DAY, -3, GETDATE()), DATEADD(DAY, 1, GETDATE()), 0, NULL, NULL, 0);
SET IDENTITY_INSERT dbo.DocumentTasks OFF;

/* ============================================================================
 * 11. МАРШРУТЫ СОГЛАСОВАНИЯ (Phase 7)
 * ========================================================================== */
SET IDENTITY_INSERT dbo.ApprovalRouteTemplates ON;
INSERT INTO dbo.ApprovalRouteTemplates (Id, Name, [Description], DocumentTypeRefId, IsActive) VALUES
    (1, N'Стандартное согласование внутренних СЗ', N'Руководитель отдела → Заместитель директора → Директор', 3, 1),
    (2, N'Согласование договоров',                  N'Юрист → Зав. складом → Зам. директора → Директор',       6, 1);
SET IDENTITY_INSERT dbo.ApprovalRouteTemplates OFF;

SET IDENTITY_INSERT dbo.ApprovalStages ON;
INSERT INTO dbo.ApprovalStages (Id, RouteTemplateId, [Order], IsParallel, ApproverEmployeeId, ApproverRole, [Description]) VALUES
    (1, 1, 1, 0, 4, NULL, N'Руководитель ИТО'),
    (2, 1, 2, 0, 2, NULL, N'Заместитель директора'),
    (3, 1, 3, 0, 1, NULL, N'Директор'),
    (4, 2, 1, 0, 3, NULL, N'Делопроизводитель / Юрист'),
    (5, 2, 2, 0, 9, NULL, N'Зав. складом'),
    (6, 2, 3, 0, 2, NULL, N'Заместитель директора'),
    (7, 2, 4, 0, 1, NULL, N'Директор');
SET IDENTITY_INSERT dbo.ApprovalStages OFF;

/* Активные маршруты (ApprovalDecision: Pending=0, Approved=1, Rejected=2, Comments=3) */
SET IDENTITY_INSERT dbo.DocumentApprovals ON;
INSERT INTO dbo.DocumentApprovals (Id, DocumentId, StageId, [Order], IsParallel, ApproverId, Decision, Comment, DecisionDate) VALUES
    (1, 3,  1,    1, 0, 4, 1, N'Согласовано без замечаний.', DATEADD(DAY, -2, GETDATE())),
    (2, 3,  2,    2, 0, 2, 0, NULL,                          NULL),
    (3, 11, NULL, 1, 0, 2, 0, N'На рассмотрении.',           NULL),
    (4, 8,  4,    1, 0, 3, 1, N'Согласовано юристом.',       DATEADD(DAY, -2, GETDATE())),
    (5, 8,  5,    2, 0, 9, 0, NULL,                          NULL);
SET IDENTITY_INSERT dbo.DocumentApprovals OFF;

/* ============================================================================
 * 12. ПРИВЯЗКА ДОКУМЕНТОВ К НОМЕНКЛАТУРЕ (Phase 7)
 * ========================================================================== */
SET IDENTITY_INSERT dbo.DocumentCaseLinks ON;
INSERT INTO dbo.DocumentCaseLinks (Id, DocumentId, NomenclatureCaseId, LinkedAt, LinkedById, IsPrimary) VALUES
    (1,  1,  2, DATEADD(DAY, -22, GETDATE()), 3,  1),
    (2,  2,  2, DATEADD(DAY,  -5, GETDATE()), 3,  1),
    (3,  3,  3, DATEADD(DAY,  -3, GETDATE()), 5,  1),
    (4,  4,  1, DATEADD(DAY, -10, GETDATE()), 1,  1),
    (5,  5,  4, DATEADD(DAY,  -8, GETDATE()), 3,  1),
    (6,  6,  3, DATEADD(DAY, -16, GETDATE()), 5,  1),
    (7,  7,  7, DATEADD(DAY,  -6, GETDATE()), 7,  1),
    (8,  8,  5, DATEADD(DAY,  -2, GETDATE()), 9,  1),
    (9,  10, 2, DATEADD(DAY,  -1, GETDATE()), 7,  1),
    (10, 12, 6, DATEADD(DAY,  -1, GETDATE()), 10, 1),
    (11, 13, 4, DATEADD(DAY,  -3, GETDATE()), 5,  1),
    (12, 14, 1, DATEADD(DAY, -50, GETDATE()), 2,  1);
SET IDENTITY_INSERT dbo.DocumentCaseLinks OFF;

/* ============================================================================
 * 13. ТРАНЗАКЦИИ ТМЦ И ПУТЕВЫЕ ЛИСТЫ (Phase 7 + Phase 15)
 * ========================================================================== */
SET IDENTITY_INSERT dbo.InventoryTransactions ON;
INSERT INTO dbo.InventoryTransactions (Id, InventoryItemId, DocumentId, QuantityChanged, TransactionDate, InitiatorId, BasisDocumentId) VALUES
    /* Приходы */
    (1, 1, NULL, 100, DATEADD(DAY, -25, GETDATE()), 9, NULL),
    (2, 2, NULL,  24, DATEADD(DAY, -20, GETDATE()), 9, NULL),
    (3, 3, NULL,  12, DATEADD(DAY, -20, GETDATE()), 9, NULL),
    (4, 4, NULL, 300, DATEADD(DAY, -25, GETDATE()), 9, NULL),
    (5, 5, NULL,  50, DATEADD(DAY, -25, GETDATE()), 9, NULL),
    (6, 6, NULL,  10, DATEADD(DAY, -25, GETDATE()), 9, NULL),
    (7, 7, NULL,  22, DATEADD(DAY, -25, GETDATE()), 9, NULL),
    /* Расходы */
    (8,  1, 6,    -10, DATEADD(DAY, -10, GETDATE()), 9, 6),
    (9,  1, 6,     -3, DATEADD(DAY,  -8, GETDATE()), 9, 6),
    (10, 4, 6,    -50, DATEADD(DAY,  -7, GETDATE()), 9, 6),
    (11, 5, 6,     -4, DATEADD(DAY,  -7, GETDATE()), 9, 6),
    (12, 6, NULL,  -1, DATEADD(DAY,  -3, GETDATE()), 9, NULL),
    (13, 2, NULL,  -7, DATEADD(DAY,  -2, GETDATE()), 9, NULL),
    (14, 3, NULL,  -4, DATEADD(DAY,  -1, GETDATE()), 9, NULL);
SET IDENTITY_INSERT dbo.InventoryTransactions OFF;

SET IDENTITY_INSERT dbo.VehicleTrips ON;
INSERT INTO dbo.VehicleTrips (Id, VehicleId, StartDate, EndDate, DocumentId, DriverName, BasisDocumentId,
                              OdometerStart, OdometerEnd, FuelIssuedLiters, Route, PassengerNames, ActualStart, ActualEnd) VALUES
    (1, 1, DATEADD(DAY,  -10, GETDATE()), DATEADD(DAY, -10, GETDATE()) + CAST('06:00:00' AS DATETIME), NULL, N'Волков С.И.', NULL,
        78100,  78180,  8.50, N'АХУ → Администрация района → склад',  NULL,             DATEADD(DAY,  -10, GETDATE()), DATEADD(DAY, -10, GETDATE()) + CAST('06:00:00' AS DATETIME)),
    (2, 2, DATEADD(DAY,   -5, GETDATE()), DATEADD(DAY,  -5, GETDATE()) + CAST('08:00:00' AS DATETIME), NULL, N'Волков С.И.', NULL,
       142080, 142220, 22.00, N'АХУ → склад → почтовое отделение',     N'Зайченко Т.А.', DATEADD(DAY,   -5, GETDATE()), DATEADD(DAY,  -5, GETDATE()) + CAST('08:00:00' AS DATETIME)),
    (3, 1, DATEADD(DAY,   -1, GETDATE()), DATEADD(DAY,  -1, GETDATE()) + CAST('04:30:00' AS DATETIME), 12,   N'Волков С.И.', 12,
        78340,  78420,  7.20, N'АХУ → Администрация района → АХУ',     N'Петрова А.С.',  DATEADD(DAY,   -1, GETDATE()), DATEADD(DAY,  -1, GETDATE()) + CAST('04:30:00' AS DATETIME)),
    (4, 4, DATEADD(HOUR,  -3, GETDATE()), DATEADD(HOUR,  5, GETDATE()),                              NULL, N'Волков С.И.', NULL,
        41100, NULL,    6.00, N'АХУ → выезд на объекты',               NULL,             DATEADD(HOUR,  -3, GETDATE()), NULL);
SET IDENTITY_INSERT dbo.VehicleTrips OFF;

/* ============================================================================
 * 14. PHASE 14 — ИТО: диагностические записи и ВКС
 * ========================================================================== */
SET IDENTITY_INSERT dbo.ItTicketDiagnosticEntries ON;
INSERT INTO dbo.ItTicketDiagnosticEntries (Id, TicketId, AuthorId, Timestamp, Action, Category) VALUES
    (1, 5,  5, DATEADD(DAY, -8, GETDATE()), N'Принят тикет от пользователей кабинета 207. Проверена видимость SSID — не транслируется.',         N'Diagnose'),
    (2, 5,  5, DATEADD(DAY, -7, GETDATE()), N'Перезагрузка точки доступа TP-Link EAP245 — после ребута SSID появился, но соединение нестабильно.', N'Workaround'),
    (3, 5,  6, DATEADD(DAY, -6, GETDATE()), N'Запрошена замена точки доступа на новую модель. Открыта внутренняя заявка на закупку.',           N'Procurement'),
    (4, 5,  5, DATEADD(DAY, -2, GETDATE()), N'Тикет просрочен — точка доступа продолжает работать нестабильно, ожидаем поставку.',             N'Status'),
    (5, 13, 5, DATEADD(DAY, -3, GETDATE()), N'Зафиксирован шум вентилятора на сервере HP DL380. Сняты логи iLO, расхождений по температуре нет.', N'Diagnose'),
    (6, 13, 5, DATEADD(DAY, -2, GETDATE()), N'Сервер передан вендору ООО «Сервис-ИТ Балашов» по договору сервисного обслуживания.',           N'VendorHandoff');
SET IDENTITY_INSERT dbo.ItTicketDiagnosticEntries OFF;

SET IDENTITY_INSERT dbo.VideoConferences ON;
INSERT INTO dbo.VideoConferences (Id, TicketId, Topic, ScheduledAt, CompletedAt, OrganizerId, Participants, Platform, MeetingUrl, Notes) VALUES
    (1, 5,    N'Совещание по нестабильному Wi-Fi (каб. 207)', DATEADD(DAY, -6, GETDATE()), DATEADD(DAY, -6, GETDATE()) + CAST('00:45:00' AS DATETIME), 5, N'Стерликов, Дорофеев, Королёв', 0, N'https://meet.example.org/wifi207',     N'Принято решение заменить точку доступа.'),
    (2, NULL, N'Планёрка ИТО — еженедельная',                  DATEADD(DAY,  1, GETDATE()), NULL,                                                       4, N'Все сотрудники ИТО',           1, N'https://meet.example.org/ito-weekly',  N'Стандартный еженедельный созвон.'),
    (3, 13,   N'Сервис-кейс по серверу HP DL380 (с вендором)', DATEADD(DAY,  2, GETDATE()), NULL,                                                       5, N'Стерликов, ООО «Сервис-ИТ»',   2, N'https://meet.example.org/hp-dl380',    N'С участием выездного инженера вендора.');
SET IDENTITY_INSERT dbo.VideoConferences OFF;

/* ============================================================================
 * 15. PHASE 15 — ИНСТРУКТАЖИ ПО ОТ, ИНВЕНТАРИЗАЦИИ, ПЕРЕДАЧА В АРХИВ
 *    SafetyBriefingKind: Introductory=0, Initial=1, Periodic=2, Unscheduled=3.
 *    InventarizationScope: Inventory=0, FixedAssets=1, ArchiveCases=2.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.SafetyBriefings ON;
INSERT INTO dbo.SafetyBriefings (Id, BriefingDate, Kind, Topic, TraineeEmployeeId, InstructorEmployeeId, SignatureConfirmed, Notes) VALUES
    (1, DATEADD(DAY, -90, GETDATE()), 0, N'Вводный инструктаж по охране труда',            5,  1, 1, NULL),
    (2, DATEADD(DAY, -90, GETDATE()), 0, N'Вводный инструктаж по охране труда',            6,  1, 1, NULL),
    (3, DATEADD(DAY, -45, GETDATE()), 1, N'Первичный инструктаж на рабочем месте',         5,  4, 1, NULL),
    (4, DATEADD(DAY, -45, GETDATE()), 1, N'Первичный инструктаж на рабочем месте',         6,  4, 1, NULL),
    (5, DATEADD(DAY, -30, GETDATE()), 2, N'Повторный инструктаж — электробезопасность',   10,  4, 1, N'Группа по электробезопасности II'),
    (6, DATEADD(DAY,  -7, GETDATE()), 3, N'Внеплановый инструктаж по работе с архивами',   8,  7, 1, N'После замены кондиционера в архивохранилище');
SET IDENTITY_INSERT dbo.SafetyBriefings OFF;

SET IDENTITY_INSERT dbo.Inventarizations ON;
INSERT INTO dbo.Inventarizations (Id, StartDate, EndDate, Scope, ScopeDescription, CommissionMembers, ChairmanId, ResultDocumentId, Notes) VALUES
    (1, DATEADD(DAY, -60, GETDATE()), DATEADD(DAY, -55, GETDATE()), 0, N'Инвентаризация ТМЦ за 1 квартал 2026',
        N'Иванова О.В. (председатель), Зайченко Т.А., Петрова А.С., Стерликов Д.Н.', 2, 14,   N'Расхождения незначительные.'),
    (2, DATEADD(DAY,  14, GETDATE()), NULL,                          1, N'Инвентаризация основных средств зданий и помещений',
        N'Иванова О.В. (председатель), Бурдина Г.Н., Зайченко Т.А.',                 2, NULL, N'Распоряжение РСП-2026-00009 — на согласовании.');
SET IDENTITY_INSERT dbo.Inventarizations OFF;

SET IDENTITY_INSERT dbo.InventarizationDiscrepancies ON;
INSERT INTO dbo.InventarizationDiscrepancies (Id, InventarizationId, ItemName, ExpectedQuantity, ActualQuantity, Reason) VALUES
    (1, 1, N'Бумага A4 «Снегурочка», 500 листов', 90.000, 87.000, N'Естественная убыль / расход на печать'),
    (2, 1, N'Картридж Canon 725',                  18.000, 17.000, N'Один картридж списан как бракованный'),
    (3, 1, N'Жидкое мыло для рук, 5л',             10.000,  9.000, N'Списан 1 поддон (бой при разгрузке)');
SET IDENTITY_INSERT dbo.InventarizationDiscrepancies OFF;

SET IDENTITY_INSERT dbo.ArchiveTransfers ON;
INSERT INTO dbo.ArchiveTransfers (Id, NomenclatureCaseId, TransferDate, TransferredById, AcceptedById, ActDocumentId, ArchiveCode, RetentionYears, Notes) VALUES
    (1, 1, DATEADD(DAY, -180, GETDATE()), 3,  7, NULL, N'Ф.1-Оп.1-Д.1', 75, N'Приказы по основной деятельности за 2025'),
    (2, 2, DATEADD(DAY, -120, GETDATE()), 3,  8, NULL, N'Ф.1-Оп.2-Д.5', 5,  N'Переписка с органами МСУ за 2025'),
    (3, 6, DATEADD(DAY,  -90, GETDATE()), 10, 7, NULL, N'Ф.1-Оп.4-Д.2', 5,  N'Путевые листы за 2025');
SET IDENTITY_INSERT dbo.ArchiveTransfers OFF;

/* ============================================================================
 * 16. PHASE 18 — Заявки на эксплуатацию и реестр основных средств
 *    MaintenanceRequest.Kind: Repair=0, HVAC=1, Electrical=2, Plumbing=3, Other=4.
 *    MaintenanceRequest.Priority: Low=0, Normal=1, High=2, Critical=3.
 *    MaintenanceRequest.Status: New=0, InProgress=1, Blocked=2, Completed=3.
 *    FixedAsset.Category: Equipment=0, Furniture=1, Vehicle=2, Other=3.
 *    FixedAsset.Status: InService=0, Maintenance=1, Decommissioned=2.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.MaintenanceRequests ON;
INSERT INTO dbo.MaintenanceRequests (Id, RegistrationDate, BuildingId, RoomId, RequesterEmployeeId, Kind, Priority, Status, Description, AssigneeEmployeeId, CompletedAt, Resolution, LinkedDocumentId) VALUES
    (1, DATEADD(DAY, -15, GETDATE()), 1, 4, 5, 0, 1, 3, N'Заменить лампу освещения в кабинете 305 (перегорела).',                                       10,   DATEADD(DAY, -14, GETDATE()), N'Лампа заменена.', NULL),
    (2, DATEADD(DAY,  -7, GETDATE()), 1, 5, 4, 1, 2, 1, N'Серверная — повышение температуры до 28°C, требуется проверка кондиционера.',                10,   NULL,                          NULL,             NULL),
    (3, DATEADD(DAY,  -2, GETDATE()), 2, 7, 7, 1, 1, 0, N'Архивохранилище А-1 — провисает дверной замок. Безопасность хранения под вопросом.',          NULL, NULL,                          NULL,             NULL),
    (4, DATEADD(DAY,  -1, GETDATE()), 1, 3, 3, 0, 0, 0, N'Канцелярия — не работает розетка возле принтера (правый ряд).',                               5,    NULL,                          NULL,             3);
SET IDENTITY_INSERT dbo.MaintenanceRequests OFF;

SET IDENTITY_INSERT dbo.FixedAssets ON;
INSERT INTO dbo.FixedAssets (Id, InventoryNumber, Name, Category, Status, AcquisitionDate, AcquisitionCost, BookValue,
                             BuildingId, RoomId, ResponsibleEmployeeId, DecommissionedAt, DecommissionDocumentId, Notes) VALUES
    (1, N'ОС-0001', N'Сервер HP ProLiant DL380 Gen10',     0, 0, DATEADD(YEAR, -3, GETDATE()), 480000.00, 320000.00, 1, 5,  4,  NULL, NULL, NULL),
    (2, N'ОС-0014', N'Коммутатор Cisco SG250-26',           0, 0, DATEADD(YEAR, -2, GETDATE()),  32000.00,  22000.00, 1, 5,  5,  NULL, NULL, NULL),
    (3, N'ОС-0033', N'Принтер Canon LBP6030 (каб. 305 #1)', 0, 0, DATEADD(YEAR, -2, GETDATE()),   8500.00,   4500.00, 1, 4,  5,  NULL, NULL, NULL),
    (4, N'ОС-0034', N'Принтер Canon LBP6030 (каб. 305 #2)', 0, 0, DATEADD(YEAR, -2, GETDATE()),   8500.00,   4500.00, 1, 4,  5,  NULL, NULL, NULL),
    (5, N'ОС-0078', N'АРМ HP EliteDesk 800 G5',             0, 0, DATEADD(YEAR, -1, GETDATE()),  58000.00,  45000.00, 1, 3,  3,  NULL, NULL, NULL),
    (6, N'ОС-0120', N'Стеллаж архивный (металл, 6 секций)', 1, 0, DATEADD(YEAR, -5, GETDATE()),  24000.00,  12000.00, 2, 7,  7,  NULL, NULL, NULL),
    (7, N'ОС-0151', N'Lada Largus универсал',                2, 0, DATEADD(YEAR, -4, GETDATE()), 720000.00, 380000.00, 3, 9,  10, NULL, NULL, N'А123БВ 64'),
    (8, N'ОС-0152', N'УАЗ Патриот',                          2, 1, DATEADD(YEAR, -7, GETDATE()), 880000.00, 220000.00, 3, 10, 10, NULL, NULL, N'Е111КХ 64 (на ТО)');
SET IDENTITY_INSERT dbo.FixedAssets OFF;

/* ============================================================================
 * 17. PHASE 11 — ЗАМЕЩЕНИЯ И ДЕЛЕГИРОВАНИЕ
 *    Стерликов в отпуске 7 дней — задачи перенаправляются Дорофееву.
 *    SubstitutionScope: Tasks=0, Approvals=1, All=2.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Substitutions ON;
INSERT INTO dbo.Substitutions (Id, OriginalEmployeeId, SubstituteEmployeeId, [From], [To], Scope, Reason, IsActive, CreatedById) VALUES
    (1, 4, 5, DATEADD(DAY,  -1, GETDATE()), DATEADD(DAY,   6, GETDATE()), 2, N'Очередной отпуск', 1, 1),
    (2, 7, 8, DATEADD(DAY, -30, GETDATE()), DATEADD(DAY, -23, GETDATE()), 0, N'Больничный',       0, 1);
SET IDENTITY_INSERT dbo.Substitutions OFF;

SET IDENTITY_INSERT dbo.TaskDelegations ON;
INSERT INTO dbo.TaskDelegations (Id, TaskId, FromEmployeeId, ToEmployeeId, DelegatedAt, Comment) VALUES
    (1, 3, 4, 5, DATEADD(DAY, -3, GETDATE()), N'Авто-делегирование по замещению Стерликов → Дорофеев');
SET IDENTITY_INSERT dbo.TaskDelegations OFF;

/* ============================================================================
 * 18. PHASE 9 — УВЕДОМЛЕНИЯ
 *    Каналы: InApp=0, Email=1, Both=2.
 *    Виды (NotificationKind): TaskAssigned=0, TaskDeadlineSoon=1, TaskOverdue=2,
 *      ApprovalRequired=3, ApprovalDecided=4, ResolutionAdded=5,
 *      DocumentRegistered=6, DocumentSigned=7, System=99.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Notifications ON;
INSERT INTO dbo.Notifications (Id, RecipientId, Kind, Title, Body, RelatedDocumentId, RelatedTaskId, RelatedApprovalId, CreatedAt, ReadAt, Channel, SentToEmailAt) VALUES
    /* admin (id=1) — 3 непрочитанных + 1 прочитанное */
    (1, 1,  6, N'Документ зарегистрирован: ВХ-2026-00037',         N'Поступил входящий запрос от Управления по делам архивов СО.',                          2,    NULL, NULL, DATEADD(DAY,  -5, GETDATE()), NULL,                         0, NULL),
    (2, 1,  7, N'Документ подписан КЭП: ПРК-2026-00007',           N'Приказ об утверждении графика отпусков подписан и заблокирован.',                       4,    NULL, NULL, DATEADD(DAY,  -8, GETDATE()), NULL,                         0, NULL),
    (3, 1, 99, N'Запущена индексация полнотекстового поиска',      N'Перестроено 8 записей AttachmentTextIndices.',                                          NULL, NULL, NULL, DATEADD(MINUTE, -15, GETDATE()), NULL,                       0, NULL),
    (4, 1,  4, N'Решение по согласованию: ДОГ-2026-00004',         N'Иванова О.В. согласовала договор поставки.',                                            8,    NULL, NULL, DATEADD(DAY,  -1, GETDATE()), DATEADD(DAY, -1, GETDATE()),  2, DATEADD(DAY, -1, GETDATE())),
    /* Стерликов (id=4) — в отпуске; уведомления копятся */
    (5, 4,  0, N'Назначено поручение по СЗ-2026-00018',            N'Принять служебную записку и распределить.',                                             3,    3,    NULL, DATEADD(DAY,  -3, GETDATE()), NULL,                         2, DATEADD(DAY, -3, GETDATE())),
    /* Дорофеев (id=5) — авто-уведомление по делегированию + просрочка */
    (6, 5,  0, N'Поручение по СЗ-2026-00018 (по замещению)',       N'Стерликов Д.Н. в отпуске — поручение пришло вам.',                                      3,    3,    NULL, DATEADD(DAY,  -3, GETDATE()), NULL,                         0, NULL),
    (7, 5,  2, N'Просрочена задача: «Не работает Wi-Fi в каб. 207»', N'Срок: ' + CONVERT(NVARCHAR(20), DATEADD(DAY, -2, GETDATE()), 120),                     5,    1,    NULL, DATEADD(DAY,  -1, GETDATE()), NULL,                         2, DATEADD(DAY, -1, GETDATE())),
    /* Бурдина (id=7) — 1 прочитанное + 1 непрочитанное */
    (8, 7,  0, N'Назначено поручение по ВХ-2026-00037',            N'Подготовить ответ в Управление по делам архивов СО.',                                   2,    NULL, NULL, DATEADD(DAY,  -5, GETDATE()), DATEADD(DAY, -4, GETDATE()),  2, DATEADD(DAY, -5, GETDATE())),
    (9, 7,  1, N'Скоро срок: ВХ-2026-00037',                       N'Срок исполнения наступит через 24 часа.',                                               2,    NULL, NULL, DATEADD(HOUR, -2, GETDATE()), NULL,                         0, NULL),
    /* Сёмина (id=8) — DeadlineSoon на задачу 2 */
    (10, 8, 1, N'Скоро срок задачи: архивный запрос Иванова И.И.', N'Срок: ' + CONVERT(NVARCHAR(20), DATEADD(HOUR, 18, GETDATE()), 120),                     7,    2,    NULL, DATEADD(MINUTE, -10, GETDATE()), NULL,                       2, DATEADD(MINUTE, -10, GETDATE())),
    /* Зайченко (id=9) — поручение по подзадаче 6 + согласование договора */
    (11, 9, 0, N'Поручение: выдать 4 картриджа Canon 725',         N'Получатель — Дорофеев А.В.',                                                            3,    6,    NULL, DATEADD(DAY,  -3, GETDATE()), DATEADD(DAY, -2, GETDATE()),  0, NULL),
    (12, 9, 3, N'Запрос на согласование: ДОГ-2026-00004',          N'Договор поставки — стадия «Зав. складом».',                                             8,    NULL, 5,    DATEADD(DAY,  -2, GETDATE()), NULL,                         2, DATEADD(DAY, -2, GETDATE())),
    /* Иванова (id=2) — Approval pending */
    (13, 2, 3, N'На согласование: РСП-2026-00009',                 N'Распоряжение об инвентаризации ТМЦ.',                                                   11,   NULL, 3,    DATEADD(DAY,  -1, GETDATE()), NULL,                         2, DATEADD(DAY, -1, GETDATE())),
    (14, 2, 3, N'На согласование: СЗ-2026-00018',                  N'Стадия «Заместитель директора».',                                                       3,    NULL, 2,    DATEADD(DAY,  -2, GETDATE()), NULL,                         0, NULL);
SET IDENTITY_INSERT dbo.Notifications OFF;

SET IDENTITY_INSERT dbo.NotificationPreferences ON;
INSERT INTO dbo.NotificationPreferences (Id, EmployeeId, Kind, Channel, IsEnabled, EmailOverride) VALUES
    (1, 1,  0, 0, 1, NULL),                          -- admin: TaskAssigned only InApp
    (2, 1,  3, 2, 1, NULL),                          -- admin: ApprovalRequired Both
    (3, 1, 99, 0, 0, NULL),                          -- admin: System notifications выключены
    (4, 4,  0, 2, 1, N'sterlikov.alt@mail.ru'),      -- Стерликов: переопределение email
    (5, 5,  2, 2, 1, NULL),                          -- Дорофеев: TaskOverdue Both
    (6, 7,  1, 2, 1, NULL),                          -- Бурдина: DeadlineSoon Both
    (7, 9,  3, 2, 1, NULL),                          -- Зайченко: ApprovalRequired Both
    (8, 2,  3, 2, 1, NULL);                          -- Иванова: ApprovalRequired Both
SET IDENTITY_INSERT dbo.NotificationPreferences OFF;

/* ============================================================================
 * 19. PHASE 8 — ПОДПИСИ И БЛОКИРОВКА
 *    SignatureKind: Simple=0, Enhanced=1, Qualified=2.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.DocumentSignatures ON;
INSERT INTO dbo.DocumentSignatures (Id, DocumentId, AttachmentId, SignerId, Kind, SignedAt, SignedHash, SignatureBlobBase64, CertificateThumbprint, CertificateSubject, CertificateNotAfter, Reason, IsRevoked, RevokedAt) VALUES
    /* Документ #1 — ПЭП директора (историческая) */
    (1, 1,  NULL, 1, 0, DATEADD(DAY, -22, GETDATE()), N'a1b2c3d4e5f607080910111213141516', N'demo-base64-bytes==', NULL, NULL, NULL, N'Согласовано', 0, NULL),
    /* Документ #4 — ПЭП Ивановой + КЭП Иванова → блокировка */
    (2, 4,  NULL, 2, 0, DATEADD(DAY, -10, GETDATE()), N'b2c3d4e5f607080910111213141516a1', N'demo-base64-bytes==', NULL, NULL, NULL, N'Согласовано (зам. директора)', 0, NULL),
    (3, 4,  5,    1, 2, DATEADD(DAY,  -8, GETDATE()), N'c3d4e5f607080910111213141516a1b2', N'demo-cades-base64==',  N'DEADBEEFDEADBEEFDEADBEEFDEADBEEFDEAD0001', N'CN=Администратор МКУ АХУ БМР, OU=Руководство, O=МКУ АХУ БМР, C=RU', DATEADD(YEAR, 1, GETDATE()), N'Утверждено директором', 0, NULL),
    /* Документ #6 — ПЭП Зайченко, отозвана позже */
    (4, 6,  NULL, 9, 0, DATEADD(DAY, -16, GETDATE()), N'd4e5f607080910111213141516a1b2c3', N'demo-base64-bytes==', NULL, NULL, NULL, N'Принято на склад',  1, DATEADD(DAY, -14, GETDATE())),
    /* Документ #8 — ПЭП Зайченко (1-я стадия согласования) */
    (5, 8,  NULL, 9, 0, DATEADD(DAY,  -2, GETDATE()), N'e5f607080910111213141516a1b2c3d4', N'demo-base64-bytes==', NULL, NULL, NULL, N'Согласовано (зав. складом, 1-я стадия)', 0, NULL),
    /* Документ #10 — ПЭП Бурдиной */
    (6, 10, NULL, 7, 0, DATEADD(DAY,  -1, GETDATE()), N'f607080910111213141516a1b2c3d4e5', N'demo-base64-bytes==', NULL, NULL, NULL, N'Согласовано', 0, NULL),
    /* Документ #14 — ПЭП Ивановой (акт инвентаризации) */
    (7, 14, NULL, 2, 0, DATEADD(DAY, -50, GETDATE()), N'070809101112131415160102030405a1', N'demo-base64-bytes==', NULL, NULL, NULL, N'Утверждено председателем комиссии', 0, NULL);
SET IDENTITY_INSERT dbo.DocumentSignatures OFF;

/* Документ #4 — заблокирован КЭП. */
UPDATE dbo.Documents SET IsLocked = 1 WHERE Id = 4;

/* ============================================================================
 * 20. PHASE 10 — ПОЛНОТЕКСТОВЫЙ ИНДЕКС И СОХРАНЁННЫЕ ПОИСКИ
 * ========================================================================== */
SET IDENTITY_INSERT dbo.AttachmentTextIndices ON;
INSERT INTO dbo.AttachmentTextIndices (Id, AttachmentId, DocumentId, ExtractedText, IndexedAt, SourceContentHash) VALUES
    (1, 1, 1,   N'Уважаемые коллеги! Направляем пояснения по статье расходов 02-04 бюджета 2026 года. Расчёты выполнены в соответствии с Приказом Минфина № 209н. Сводная таблица приложена. С уважением, Петрова А.С.',
        DATEADD(DAY, -22, GETDATE()), N'h-c1a1'),
    (2, 2, 2,   N'В целях контроля за работой архивных подразделений просим в срок до 28 апреля 2026 года направить в адрес Управления по делам архивов сведения по работе с обращениями граждан за 1 квартал 2026 года.',
        DATEADD(DAY,  -5, GETDATE()), N'h-c2a2'),
    (3, 3, 3,   N'Прошу заменить тонер-картриджи Canon 725 в количестве 4 штук в принтерах Canon LBP6030, расположенных в кабинете 305 (служба ИТО). Картриджи израсходованы при печати квартальной отчётности.',
        DATEADD(DAY,  -3, GETDATE()), N'h-c3a3'),
    (4, 4, 4,   N'Утвердить график очередных оплачиваемых отпусков сотрудников МКУ АХУ Балашовского муниципального района на 2026 год согласно приложению. Контроль исполнения возложить на отдел кадров.',
        DATEADD(DAY, -10, GETDATE()), N'h-c4a4'),
    (5, 6, 6,   N'Прошу выдать на отдел службы информационно-технического обеспечения 10 пачек бумаги формата А4 для печати квартальной отчётности 2026 года. Бумага необходима срочно — текущий запас закончился.',
        DATEADD(DAY, -16, GETDATE()), N'h-c6a6'),
    (6, 8, 8,   N'Договор поставки канцелярских товаров № 04-2026 заключён между МКУ АХУ Балашовского муниципального района и ООО «Канцоптторг». Предмет договора — поставка канцелярских товаров (бумага, ручки, маркеры, скрепки) на 2 квартал 2026 года. Цена договора — 124 500 рублей.',
        DATEADD(DAY,  -2, GETDATE()), N'h-c8a8'),
    (7, 9, 10,  N'В ответ на ваш запрос ВХ-2026-00037 направляем сводку по обработанным архивным запросам граждан за 1 квартал 2026 года: всего 47 запросов, выдано 41 справка, отказано в 6 случаях.',
        DATEADD(DAY,  -2, GETDATE()), N'h-c9a9'),
    (8, 10, 14, N'Акт инвентаризации товарно-материальных ценностей за 1 квартал 2026 года. Выявлены незначительные расхождения: бумага A4 (-3 пачки), картриджи Canon 725 (-1 шт.), мыло жидкое (-1 поддон). Списано в установленном порядке.',
        DATEADD(DAY, -50, GETDATE()), N'h-c10a10');
SET IDENTITY_INSERT dbo.AttachmentTextIndices OFF;

SET IDENTITY_INSERT dbo.SavedSearches ON;
INSERT INTO dbo.SavedSearches (Id, OwnerId, Name, FilterJson, IsShared, CreatedAt) VALUES
    (1, 1, N'Входящие письма за месяц',
        N'{"Direction":1,"PeriodFrom":"' + CONVERT(NVARCHAR(10), DATEADD(MONTH, -1, GETDATE()), 23) + N'","PeriodTo":"' + CONVERT(NVARCHAR(10), GETDATE(), 23) + N'"}',
        1, DATEADD(DAY, -10, GETDATE())),
    (2, 1, N'Просроченные задачи моего отдела',
        N'{"OnlyOverdue":true,"DepartmentId":2}',
        1, DATEADD(DAY,  -5, GETDATE())),
    (3, 4, N'Мои подписанные договоры',
        N'{"DocumentTypeRefId":6,"AssignedEmployeeId":4,"OnlySigned":true}',
        0, DATEADD(DAY,  -3, GETDATE())),
    (4, 7, N'Архивные запросы кв. 1 2026',
        N'{"DocumentTypeRefId":8,"PeriodFrom":"' + CONVERT(NVARCHAR(10), DATEADD(MONTH, -3, GETDATE()), 23) + N'"}',
        1, DATEADD(DAY,  -7, GETDATE()));
SET IDENTITY_INSERT dbo.SavedSearches OFF;

/* ============================================================================
 * 21. PHASE 16 — История паролей и журнал попыток входа
 *    LoginAttempts.FailureReason: None=0, BadCredentials=1, UnknownUser=2, Locked=3.
 * ========================================================================== */
INSERT INTO dbo.EmployeePasswordHistories (EmployeeId, PasswordHash, SetAt) VALUES
    (1, @pwHash, DATEADD(DAY, -30, GETDATE())),
    (2, @pwHash, DATEADD(DAY, -45, GETDATE())),
    (4, @pwHash, DATEADD(DAY, -60, GETDATE())),
    (5, @pwHash, DATEADD(DAY, -20, GETDATE())),
    (7, @pwHash, DATEADD(DAY, -25, GETDATE())),
    (9, @pwHash, DATEADD(DAY, -35, GETDATE()));

INSERT INTO dbo.LoginAttempts (EmployeeId, AttemptedFullName, Timestamp, IpAddress, Success, FailureReason) VALUES
    (1,    N'Администратор',                     DATEADD(MINUTE, -45, GETDATE()), N'192.168.20.50',  1, 0),
    (3,    N'Петрова Анна Сергеевна',            DATEADD(MINUTE, -30, GETDATE()), N'192.168.20.78',  1, 0),
    (5,    N'Дорофеев Артём Валерьевич',         DATEADD(HOUR,    -2, GETDATE()), N'192.168.20.45',  1, 0),
    (7,    N'Бурдина Галина Николаевна',         DATEADD(HOUR,    -1, GETDATE()), N'192.168.30.12',  1, 0),
    (NULL, N'sidorov',                           DATEADD(HOUR,    -4, GETDATE()), N'192.168.90.250', 0, 1),
    (NULL, N'unknown',                           DATEADD(HOUR,    -6, GETDATE()), N'10.0.0.42',       0, 2),
    (4,    N'Стерликов Дмитрий Николаевич',      DATEADD(DAY,     -1, GETDATE()), N'192.168.20.30',  0, 1);

/* ============================================================================
 * 22. ЖУРНАЛ АУДИТА (Phase 7)
 *    Hash / PreviousHash — NULL: пересчитает приложение при следующей операции.
 *    AuditActionType: Created=0, StatusChanged=10, Registered=11, AttachmentAdded=20,
 *      AttachmentVersioned=21, ResolutionIssued=30, TaskAssigned=31, TaskCompleted=32,
 *      TaskOverdue=33, TaskReassigned=34, ApprovalSent=40, ApprovalSigned=41,
 *      SignatureAdded=60, SignatureRevoked=61, DocumentLocked=62,
 *      NotificationSent=70, SubstitutionCreated=80, TaskDelegated=82,
 *      IndexRebuilt=85, ReportGenerated=86, UserLogin=90.
 * ========================================================================== */
INSERT INTO dbo.AuditLogs (Timestamp, UserId, ActionType, EntityType, EntityId, OldValues, NewValues, Details, Hash, PreviousHash) VALUES
    (DATEADD(DAY, -25, GETDATE()), 3, 0,  N'Document', 1,  NULL, NULL, N'Создан исходящий документ ИСХ-2026-00012', NULL, NULL),
    (DATEADD(DAY, -22, GETDATE()), 1, 11, N'Document', 1,  NULL, N'{"RegistrationNumber":"ИСХ-2026-00012"}', N'Документ зарегистрирован', NULL, NULL),
    (DATEADD(DAY, -22, GETDATE()), 1, 60, N'DocumentSignature', 1, NULL, N'{"Kind":"Simple"}', N'ПЭП директора', NULL, NULL),
    (DATEADD(DAY, -10, GETDATE()), 1, 0,  N'Document', 4,  NULL, NULL, N'Создан приказ ПРК-2026-00007', NULL, NULL),
    (DATEADD(DAY, -10, GETDATE()), 1, 11, N'Document', 4,  NULL, N'{"RegistrationNumber":"ПРК-2026-00007"}', N'Приказ зарегистрирован', NULL, NULL),
    (DATEADD(DAY, -10, GETDATE()), 2, 60, N'DocumentSignature', 2, NULL, N'{"Kind":"Simple"}', N'ПЭП заместителя директора', NULL, NULL),
    (DATEADD(DAY,  -8, GETDATE()), 1, 60, N'DocumentSignature', 3, NULL, N'{"Kind":"Qualified"}', N'КЭП директора', NULL, NULL),
    (DATEADD(DAY,  -8, GETDATE()), 1, 62, N'Document', 4,  NULL, N'{"IsLocked":true}', N'Документ заблокирован после КЭП', NULL, NULL),
    (DATEADD(DAY,  -8, GETDATE()), 3, 0,  N'Document', 5,  NULL, NULL, N'Создан тикет ИТО-2026-00091', NULL, NULL),
    (DATEADD(DAY,  -7, GETDATE()), 1, 31, N'DocumentTask', 1, NULL, N'{"ExecutorId":5}', N'Назначено поручение Дорофееву', NULL, NULL),
    (DATEADD(DAY,  -5, GETDATE()), 3, 11, N'Document', 2,  NULL, N'{"RegistrationNumber":"ВХ-2026-00037"}', N'Зарегистрировано входящее письмо', NULL, NULL),
    (DATEADD(DAY,  -5, GETDATE()), 1, 30, N'DocumentResolution', 1, NULL, N'{"AuthorId":1}', N'Резолюция директора', NULL, NULL),
    (DATEADD(DAY,  -5, GETDATE()), 1, 31, N'DocumentTask', NULL, NULL, NULL, N'Поручение Бурдиной по ВХ-2026-00037', NULL, NULL),
    (DATEADD(DAY,  -5, GETDATE()), 1, 70, N'Notification', 8, NULL, NULL, N'Уведомление Бурдиной отправлено по in-app + email', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 5, 0,  N'Document', 3,  NULL, NULL, N'Черновик СЗ о картриджах', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 5, 11, N'Document', 3,  NULL, N'{"RegistrationNumber":"СЗ-2026-00018"}', N'СЗ зарегистрирована', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 4, 31, N'DocumentTask', 3, NULL, N'{"ExecutorId":4}', N'Поручение Стерликову (в отпуске)', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 1, 82, N'TaskDelegation', 1, NULL, N'{"To":5}', N'Авто-делегирование Стерликов→Дорофеев', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 1, 80, N'Substitution', 1, NULL, N'{"Original":4,"Substitute":5}', N'Создано замещение на отпуск', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 4, 40, N'DocumentApproval', 1, NULL, N'{"StageId":1}', N'Запущен маршрут согласования', NULL, NULL),
    (DATEADD(DAY,  -2, GETDATE()), 4, 41, N'DocumentApproval', 1, NULL, N'{"Decision":"Approved"}', N'Согласовано Стерликовым (1-я стадия)', NULL, NULL),
    (DATEADD(DAY,  -2, GETDATE()), 9, 60, N'DocumentSignature', 5, NULL, N'{"Kind":"Simple"}', N'ПЭП Зайченко по договору', NULL, NULL),
    (DATEADD(DAY,  -2, GETDATE()), 7, 0,  N'Document', 10, NULL, NULL, N'Создано исх. ИСХ-2026-00021 (ответ на ВХ-2026-00037)', NULL, NULL),
    (DATEADD(DAY,  -2, GETDATE()), 9, 21, N'DocumentAttachment', 8, NULL, N'{"VersionNumber":2}', N'Новая версия договора (юрист)', NULL, NULL),
    (DATEADD(DAY,  -1, GETDATE()), 1, 85, N'AttachmentTextIndex', NULL, NULL, NULL, N'Полнотекстовый индекс пересобран', NULL, NULL),
    (DATEADD(DAY,  -1, GETDATE()), 7, 60, N'DocumentSignature', 6, NULL, N'{"Kind":"Simple"}', N'ПЭП Бурдиной по ИСХ-2026-00021', NULL, NULL),
    (DATEADD(DAY,  -1, GETDATE()), 5, 33, N'DocumentTask', 1, NULL, NULL, N'Задача ИТО-2026-00091 перешла в Overdue', NULL, NULL);

PRINT N'AhuErpDb: демо-данные загружены.';
PRINT N'Учётные записи: admin / sterlikov / dorofeev / burdina / zaychenko / volkov / petrova';
PRINT N'Пароль: password';
GO
