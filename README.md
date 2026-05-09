# AhuErp — ERP/СЭД для МКУ «АХУ» БМР

Информационная система административно-хозяйственного управления и
электронного документооборота для **МКУ «АХУ» БМР** (Балаковский муниципальный
район). Покрывает ключевые направления деятельности учреждения:
делопроизводство (РКК с полным жизненным циклом), архивный отдел, склад/ТМЦ,
ИТО (Help Desk), транспортный отдел, оргструктура и замещения, аналитика и
регламентные отчёты.

**Стек:** .NET Framework 4.8 (SDK-style csproj), WPF + MVVM
(`CommunityToolkit.Mvvm` 8.3), Entity Framework 6.4.4 Code-First +
12 миграций, xUnit 2.9, ClosedXML 0.102, DocumentFormat.OpenXml 2.20,
LiveCharts.Wpf 0.9.7, PdfPig (полнотекстовая индексация PDF), PBKDF2
для парольных хэшей, Microsoft.Extensions.DependencyInjection.

**Размер:** ~25 ViewModel-ей, ~25 Views, ~30 моделей домена, ~30 сервисов
(`AhuErp.Core/Services`), 12 EF6-миграций. Тесты: **235 / 235 passed**
на `main` (CI зелёный после релиза приёмочных фиксов; ветки текущих багфиксов
поднимают эту цифру до 244 — см. раздел Roadmap).

---

## Архитектура

```
AhuErp.sln
├── src/
│   ├── AhuErp.Core/            ← .NET 4.8, SDK-style class library
│   │   ├── Models/             ← домен: Document (TPH: ArchiveRequest, ItTicket),
│   │   │                         Employee, Department, NomenclatureCase, DocumentTask,
│   │   │                         DocumentResolution, DocumentApproval, DocumentSignature,
│   │   │                         DocumentAttachment, AuditLog, Notification,
│   │   │                         NotificationPreference, Substitution, TaskDelegation,
│   │   │                         InventoryItem, InventoryTransaction, Vehicle, VehicleTrip,
│   │   │                         AttachmentTextIndex, SavedSearch, …
│   │   ├── Data/AhuDbContext.cs← EF6 контекст, TPH-маппинги, FK-конфигурация
│   │   ├── Migrations/         ← 12 EF6 миграций (Phase 1 → 12) + .resx-снимки
│   │   └── Services/           ← бизнес-логика: AuthService, RolePolicy,
│   │                             ApprovalService, SignatureService, TaskService,
│   │                             NomenclatureService, InventoryService, FleetService,
│   │                             ArchiveService, NotificationService, SearchIndexService,
│   │                             SavedSearchService, ReportService, AuditService,
│   │                             SubstitutionService, DelegationService,
│   │                             AttachmentService, WorkflowService, DashboardService,
│   │                             + `Ef*Repository` (production) и `InMemory*Repository`
│   │                             (для тестов и быстрых демонстраций)
│   └── AhuErp.UI/              ← .NET 4.8 SDK-style WPF Application (UseWPF=true)
│       ├── App.xaml(.cs)       ← bootstrapping: DI → LoginWindow → MainWindow
│       │                         + DispatcherTimer-ы (TickReminders, IndexOutdated)
│       ├── Infrastructure/     ← AppServices (DI-композишн-рут), EfDataSeeder,
│       │                         DemoDataSeeder, FileDialogService, DocumentNavigator
│       ├── ViewModels/         ← MVVM на CommunityToolkit.Mvvm
│       ├── Views/              ← XAML-формы по разделам навигации
│       ├── Messaging/          ← IMessenger-сообщения (UnreadCountChangedMessage и др.)
│       └── Converters/         ← BooleanToVisibility, EnumDisplay,
│                                 OverdueRowColor, InventoryDeltaSign, …
└── tests/
    └── AhuErp.Tests/           ← xUnit, 235 тестов на main
```

Все три проекта — SDK-style `.csproj` с `TargetFramework=net48`, что позволяет
`dotnet build` / `dotnet test` / `dotnet format` работать **без Visual Studio**
(в том числе на CI под Linux через
`Microsoft.NETFramework.ReferenceAssemblies 1.0.3`). EF6 контекст — singleton,
живёт всё время сессии WPF-приложения (UI-однопоточное, обращения с UI-нити).

---

## Модули и роли

`MainViewModel` строит панель навигации из 17 разделов и фильтрует их через
`RolePolicy.IsAllowed(role, moduleKey)`.

| #  | Раздел                       | Роли с доступом                                     | Что внутри                                                                 |
|----|------------------------------|-----------------------------------------------------|----------------------------------------------------------------------------|
| 1  | Мой рабочий стол             | Admin, Manager, Archivist, TechSupport, WhMgr       | KPI-карточки + лента уведомлений + последние документы по сотруднику       |
| 2  | Дашборд                      | Admin, Manager, Archivist, TechSupport, WhMgr       | KPI + LiveCharts (PieChart по статусам, ColumnChart по категориям ТМЦ)     |
| 3  | РКК (документы)              | Admin, Manager, WhMgr                               | Полная регистрационно-контрольная карточка: 6 вкладок                      |
| 4  | Документационное обеспечение | Admin, Manager, Archivist, TechSupport, WhMgr       | Реестр входящих/внутренних документов                                      |
| 5  | Мои задачи                   | Admin, Manager, Archivist, TechSupport, WhMgr       | Поручения текущему пользователю с дедлайнами и контролем                   |
| 6  | Архивный отдел               | Admin, Manager, Archivist                           | Заявки граждан, архивные справки (DOCX), сроки 7/15/30 дней                |
| 7  | Склад / ТМЦ                  | Admin, Manager, WhMgr                               | Остатки, приход/расход с привязкой к документу-основанию, фильтры          |
| 8  | ИТО                          | Admin, Manager, TechSupport                         | Help Desk: `ItTicket`, разрешение с опциональным списанием расходников     |
| 9  | Транспорт                    | Admin, Manager, WhMgr                               | Парк, расписание, бронирование с проверкой пересечений (Allen)             |
| 10 | Номенклатура дел             | Admin, Manager, Archivist                           | Дела по индексам, сроки хранения по перечню                                |
| 11 | Журналы регистрации          | Admin, Manager, Archivist                           | Реестры по типам документов (входящие, исходящие, внутренние, и т.п.)      |
| 12 | Поиск                        | все роли                                            | Полнотекстовый поиск по вложениям + сохранённые фильтры                    |
| 13 | Отчёты                       | Admin, Manager, Archivist, WhMgr                    | Регламентные отчёты (XLSX/DOCX/PDF)                                        |
| 14 | Оргструктура                 | Admin                                               | Иерархия отделов + назначение руководителей                                |
| 15 | Замещения                    | Admin, Manager, Archivist, TechSupport, WhMgr       | Активные замещения сотрудников + автоперенаправление задач                 |
| 16 | Уведомления (настройки)      | Admin, Manager, Archivist, TechSupport, WhMgr       | Включение/отключение каналов (in-app / email) по типам событий             |
| 17 | Журнал аудита                | Admin                                               | Хэш-цепочка событий (доступ, экспорт, подписи, изменения)                  |

Роли: `Admin`, `Manager`, `Archivist`, `TechSupport`, `WarehouseManager`.
Дополнительные поведенческие проверки в `RolePolicy.Can*(role)`:
`CanSign`, `CanSignQualified`, `CanManageOrgStructure`, `CanCreateSubstitution`,
`CanViewReports`, `CanCancelRelatedOperation`, `CanRebuildSearchIndex`,
`CanFullTextSearch`, `CanManageSavedSearches`, `CanManageNotificationPrefs`,
`CanIssueResolution`.

---

## Phases (хронология реализации)

Для ориентира ниже краткая шкала, как нарастал функционал. Подробный diff —
в конкретных миграциях `src/AhuErp.Core/Migrations/` и истории коммитов.

| Phase | Тема                                                | Ключевые артефакты                                                                              |
|-------|-----------------------------------------------------|-------------------------------------------------------------------------------------------------|
| 1     | Foundation                                          | `AhuDbContext`, `Document/ArchiveRequest/Vehicle/VehicleTrip`, `ArchiveService`, `FleetService`, `DashboardService`, миграция `InitialCreate` |
| 2     | DI + аутентификация + CRUD-экраны                   | `Microsoft.Extensions.DependencyInjection`, `IAuthService`, PBKDF2 `Pbkdf2PasswordHasher`, `EmployeeRole`, `RolePolicy`, миграция `AddEmployeeAuth` |
| 3     | Склад / ТМЦ + ИТО (Help Desk)                       | `InventoryItem`, `InventoryTransaction`, `ItTicket` (TPH), `IInventoryService`, миграция `AddInventoryAndItTicket` |
| 4     | Транспорт                                           | `IVehicleRepository`, перегрузка `FleetService.BookVehicle(vehicleId, documentId, …)`, миграция `AddVehicleTripDriverName` |
| 5     | Аналитика и экспорт отчётов                         | `IReportService` (XLSX через ClosedXML, DOCX через OpenXml), `IFileDialogService`, KPI-карточки и LiveCharts на `DashboardView` |
| 6     | EF6 в production                                    | Все четыре репозитория (`EfDocumentRepository`, `EfEmployeeRepository`, `EfInventoryRepository`, `EfVehicleRepository`) подменяют in-memory; в `AppServices` регистрируется `AhuDbContext` (singleton) |
| 7     | Промышленный СЭД-уровень                            | Справочники `Departments` / `DocumentTypeRefs` / `NomenclatureCases`; вложения с версиями (`DocumentAttachment`); резолюции и поручения (`DocumentResolution`, `DocumentTask`); маршруты согласования (`ApprovalRouteTemplate` / `ApprovalStage` / `DocumentApproval`); журнал аудита (`AuditLog`) с hash-цепочкой; ссылка `BasisDocumentId`; миграция `AddEnterpriseEDMSFeatures` |
| 8     | Электронные подписи + блокировка документа          | `DocumentSignature` + `SignatureKind` (Simple/Qualified), `ICryptoProvider` (HMAC для Simple, `CryptoProStub` для Qualified-канала), `DocumentLockGuard`, миграция `AddSignatures` |
| 9     | Уведомления + рабочий стол                          | `Notification`, `NotificationPreference`, `INotificationService` (in-app + e-mail через `IEmailGateway`), `MyDesktopViewModel`, кликабельный бейдж непрочитанных в шапке, `DispatcherTimer` `TickReminders`, миграция `AddNotifications` |
| 10    | Полнотекстовый поиск                                | `AttachmentTextIndex`, `SavedSearch`, `SearchIndexService`, экстракторы `Pdf` / `Docx` / `PlainText`, фоновый `IndexOutdated` каждые 5 мин, миграция `AddSearchIndex` |
| 11    | Оргструктура + замещения + делегирование задач      | Иерархия `Department`, `Substitution`, `TaskDelegation`, `SubstitutionService` подменяет исполнителя в `TaskService` / `ApprovalService` при активном замещении, миграция `AddOrgAndSubstitution` |
| 12    | Регламентные отчёты + локализация UI                | Отчёты в XLSX / DOCX / PDF, русские подписи всех enum в UI и отчётах, `EnumDisplayConverter` |
| 13    | Жизненный цикл документа по гос-делопроизводству     | `DocumentStatus` расширен до 11 значений (`Draft/Registered/OnApproval/Approved/Rejected/OnSigning/Signed/OnExecution/Completed/Cancelled/Archived`); `DocumentStateMachine.CanTransition(from,to[,role])` со «строгой» матрицей переходов и ролевыми ограничениями (Admin может всё валидное по графу; передача в архив — только Archivist/Manager/DeputyHead); `DocumentStateMachine.Transition` атомарно меняет статус и пишет `AuditActionType.StatusChanged`; интеграция в `NomenclatureService.Register` (Draft→Registered), `ApprovalService.StartApproval` (→OnApproval) и `ApplyDecision` (→Approved/Rejected), `SignatureService.Sign(Qualified)` (→Signed); `DocumentFilter.DocumentStatusFacet` дополнен `Rejected/OnSigning/Signed/Archived`; русская локализация новых статусов в `EnumDisplayConverter` |
| 14    | Расширение модуля ИТО (системный администратор)      | Каталог `Equipment` (инв. №, тип/статус/MAC/IP/кабинет/ответственный/гарантия), справочник `NetworkSegment` (VLAN/диапазон/маска/шлюз/DNS), журнал `VideoConference` (тема/площадка Zoom/Jitsi/RegionalVks/ссылка/организатор/участники), хронологический `ItTicketDiagnosticEntry`, расширение `ItTicket` (`Kind`, `AffectedEquipmentId` FK, `IsSentToVendor` + поставщик/№ заявки/срок возврата, `CompletedAt`); KPI-плитки на дашборде ИТО (`IItServiceMetricsProvider`: Open/InProgress/Overdue/SentToVendor/CompletedCount/MTTR); миграция `AddItoExpansionPhase14` (4 новые таблицы + 6 колонок к `Documents`) |
| 16    | Безопасность и админ-панель (Bug #8 + Improvement #17) | Парольная политика `IPasswordPolicy` (мин 8 / 1 цифра / 1 заглавная / 1 строчная, 90-дневный срок действия, история последних 5 паролей), `LoginAttempt` (журнал входов с IP, временем, причиной отказа), lockout 5 неудач за 10 минут → блокировка на 30 минут (`Employee.LockedUntil`), AES-256-CBC + HMAC-SHA256 шифрование чувствительных полей через `IDocumentEncryptor` / `AesDocumentEncryptor` (формат `enc:v1:<base64>`), singleton `OrganizationSettings` с настраиваемыми порогами и шифр-ключом, расширенные действия в `AuditActionType` (`LoginAttemptFailed`/`AccountLocked`/`PasswordChanged`/`PasswordExpired`/`AccountUnlocked`/`ConfidentialDocumentViewed`/`DocumentExportedToExcel`/`DocumentExportedToPdf`/`DocumentPrinted`/`DocumentDownloaded`/`RoleChanged`/`SubstitutionChanged`/`EncryptionKeyRotated`), `AttachmentService.Download` с явным `DocumentDownloaded` audit, миграция `AddSecurityAndAdminPhase16` (3 новые таблицы + 2 колонки в `Employees`) |

Дополнительная миграция `AddInventoryItemUnitAndMinimumBalance` добавляет
поля единиц измерения и минимального остатка к `InventoryItem`.

### Phase 13 — машина состояний документа

Жизненный цикл соответствует ГОСТ Р 7.0.97-2016 и муниципальному
делопроизводству. Допустимые переходы:

```
Draft (New) ───┬──► Registered ──┬──► OnApproval ──┬──► Approved ──┬──► OnSigning ──► Signed ──┬──► OnExecution ──┬──► Completed ──► Archived
               │                 │                 │                │                          │                  │
               │                 │                 │                ├──► OnExecution           ├──► Completed     │
               │                 │                 │                ├──► Completed             ├──► Cancelled     │
               │                 │                 │                └──► Cancelled             └──► OnHold ◄──────┘ (legacy)
               │                 │                 │
               │                 │                 └──► Rejected ──► Draft (на доработку) | Cancelled
               │                 │
               │                 └──► OnSigning | OnExecution | Completed | Cancelled | Archived
               │
               └──► OnApproval | Cancelled
```

`Cancelled` и `Archived` — терминальные. `InProgress` (legacy Phase 1-12) —
синоним `OnExecution`, поддерживается для обратной совместимости при
чтении старых записей.

Ролевые ограничения (помимо логического графа):
- **Admin** — все валидные по графу переходы (для расследований и
  ручных коррекций).
- **Manager / DeputyHead / Clerk / Archivist** — основной офисный поток
  (регистрация, запуск согласования, отмена, передача в архив).
- **Archivist** — монополия на `Completed → Archived` (помимо
  Manager/DeputyHead).
- **TechSupport / WarehouseManager / FleetManager** — могут завершать
  свои `OnExecution` документы (`→ Completed` / `→ OnHold` / `→ Cancelled`).
- **HRAdmin** — не управляет статусами документов.

Любой переход через `DocumentStateMachine.Transition` пишет
`AuditActionType.StatusChanged` с `OldValues=Status=From; NewValues=Status=To`
и опциональным `Details` (причина), что покрывает требования к журналу
аудита.

### Phase 14 — расширение модуля ИТО (системный администратор)

Покрывает должностную инструкцию системного администратора МКУ «АХУ» БМР
(мелкий ремонт, диагностика и передача в сервис, установка/сопровождение
ПО, ВКС, администрирование сайта, настройка сети). Раздел «ИТО»
переработан под помощника инженера: KPI-дашборд, каталог техники, журнал
диагностики, передача внешнему поставщику, журнал ВКС.

**Новые модели (`AhuErp.Core/Models`):**
- `Equipment` — каталог техники: `InventoryNumber`, `Type`
  (`EquipmentType`: Pc/Printer/Switch/AccessPoint/IpPhone/IpCamera/Server/
  VideoConferenceUnit/Ups/Other), `Model`, `SerialNumber`, `MacAddress`,
  `IpAddress`, `Room`, `ResponsibleEmployeeId`, `InServiceDate`,
  `WarrantyExpiry`, `Status` (`EquipmentStatus`:
  Working/InRepair/Decommissioned), `NetworkSegmentId`, `Notes`.
- `NetworkSegment` — справочник сегментов сети: `Name`, `Vlan`,
  `IpRange`, `SubnetMask`, `Gateway`, `Dns`, `Notes`. Привязывается к
  `Equipment.NetworkSegmentId`.
- `VideoConference` — журнал ВКС: `Topic`, `ScheduledAt`, `CompletedAt`,
  `OrganizerId`, `Participants`, `Platform`
  (`VideoConferencePlatform`: Zoom/Jitsi/RegionalVks), `MeetingUrl`,
  `Notes`, `TicketId` (FK на `ItTicket` для подготовительной заявки).
- `ItTicketDiagnosticEntry` — хронологический журнал диагностики:
  `TicketId`, `AuthorId`, `Timestamp`, `Action`, `Category`. Каждое
  действие («Перезагрузил роутер», «Заменил патч-корд», «Передал в
  сервис») сохраняется отдельной записью.

**Расширение `ItTicket` (Phase 14):**
- `Kind` (`ItTicketKind`: HardwareRepair/SoftwareInstall/NetworkConfig/
  VideoConference/WebsiteAdmin/UserConsult) — классификатор типа заявки.
- `AffectedEquipmentId` (FK → `Equipment`) + `AffectedEquipmentRef`
  (legacy `AffectedEquipment` строкой оставлен для обратной совместимости).
- `IsSentToVendor` + `VendorName` + `VendorTicketNumber` +
  `VendorReturnDeadline` — передача в сервис внешнему поставщику.
  Семантически статус = `OnHold` (ожидает внешнего действия) +
  `IsSentToVendor=true`.
- `CompletedAt` — момент закрытия заявки (для расчёта MTTR).
- `DiagnosticEntries` — навигационная коллекция к
  `ItTicketDiagnosticEntry`.

**Сервисы и репозитории:**
- `IEquipmentRepository` / `INetworkSegmentRepository` /
  `IVideoConferenceRepository` / `IItTicketDiagnosticRepository` —
  стандартный CRUD, EF6 + InMemory реализации.
- `IItServiceMetricsProvider` → `ItServiceMetricsProvider` — считает
  KPI из `IDocumentRepository.ListItTickets()`:
  `OpenCount`/`InProgressCount`/`OverdueCount`/`SentToVendorCount`/
  `CompletedCount`/`MeanTimeToResolve` (среднее
  `CompletedAt - CreationDate` по закрытым заявкам).

**UI (`ItServiceView` / `ItServiceViewModel`):**
- Полоса из 5 KPI-плиток (Открытых / В работе / Просрочено / У поставщика /
  Среднее время решения) с авто-пересчётом через `RecomputeKpi()` после
  каждой загрузки.
- TabControl «Заявки / Каталог техники / Журнал диагностики» с
  отдельными гридами и формами добавления.
- Карточка заявки: блок «Передача в сервис» (поставщик, № заявки,
  срок возврата) появляется при `IsSentToVendor=true`; кнопки
  «Передать в сервис» / «Вернуть из сервиса» меняют `IsSentToVendor` и
  `DocumentStatus` (OnHold ⇄ InProgress) с записью в журнал диагностики.
- Формат MTTR: `«1 д 04:30»` или `«04:30»` (метод `FormatMttr`).

**Миграция `AddItoExpansionPhase14`:**
- Создаёт таблицы `Equipment`, `NetworkSegments`, `VideoConferences`,
  `ItTicketDiagnosticEntries` со всеми FK.
- Расширяет `Documents` колонками `AffectedEquipmentId`, `Kind`,
  `IsSentToVendor`, `VendorName`, `VendorTicketNumber`,
  `VendorReturnDeadline`, `CompletedAt`.
- FK с `ON DELETE NO ACTION` на `Equipment` (от `Documents` и от
  `NetworkSegments`).

**RBAC:** доступ к разделу «ИТО» — `Admin / Manager / TechSupport /
DeputyHead` (без изменений в политике, расширение существующего раздела).

### Phase 16 — безопасность и админ-панель (Bug #8 + Improvement #17)

Закрывает требование «полный AuditLog + парольная политика + журнал
входов + шифрование чувствительных полей» из акта приёмки. Введены
доменные сервисы и хранилища для админ-панели; UI самой панели вынесен в
follow-up — backend полностью готов и покрыт юнит-тестами.

**Парольная политика (`IPasswordPolicy` → `PasswordPolicy`):**
- Минимальная длина 8 символов (настраивается через
  `OrganizationSettings.PasswordMinLength`).
- Хотя бы одна цифра, одна заглавная и одна строчная буква.
- Срок действия 90 дней (`OrganizationSettings.PasswordExpiryDays`),
  по истечении — отказ во входе с `LoginFailureReason.PasswordExpired`.
- История последних 5 паролей (`PasswordHistoryDepth`) — повторное
  использование запрещено через `ValidateAgainstHistory(...)`.
- При смене пароля старый хэш пишется в `EmployeePasswordHistories`,
  лишние записи усекаются `TrimToDepth`.

**Журнал входов и lockout (`LoginAttempt` + `AuthService`):**
- Каждая попытка логина пишется в `LoginAttempts` с полями
  `EmployeeId / Timestamp / IpAddress / Success /
  FailureReason` (`AccountLocked / AccountInactive / WrongPassword /
  PasswordExpired / UnknownUser / NoPasswordSet`).
- При 5 неудачах за 10 минут (`LockoutFailureThreshold` /
  `LockoutWindowMinutes`) — `Employee.LockedUntil =
  now + LockoutDurationMinutes` (по умолчанию 30 минут) и аудит
  `AccountLocked`.
- Истёкшая блокировка автоматически снимается при следующем успешном
  входе.
- IP-адрес берётся из `AuthService.ipProvider`-делегата, чтобы UI и
  тесты могли подменять источник (без хардкода `Environment.MachineName`).

**Шифрование чувствительных полей (`IDocumentEncryptor` →
`AesDocumentEncryptor`):**
- Алгоритм: AES-256-CBC + HMAC-SHA256 (encrypt-then-MAC).
- 32-байтный мастер-ключ хранится в `OrganizationSettings.EncryptionKey`
  (base64); из него HKDF-подобной схемой
  (`SHA-256(master || label)`) выводятся отдельные ключи для AES и
  HMAC, чтобы один ключ не использовался под две операции.
- Формат шифротекста: `enc:v1:<base64(iv|cipher|hmac)>` —
  `IsEncryptedPayload` отличает зашифрованные значения от legacy
  plaintext, что нужно для миграции существующих `Document.Summary` без
  единомоментного rewrite.
- `RotateKey()` генерирует новый 32-байтный ключ через
  `RandomNumberGenerator`, сохраняет его и `EncryptionKeyGeneratedAt`,
  пишет аудит `EncryptionKeyRotated` (вызывается из админ-панели).
- Если ключ не сконфигурирован (`IsEnabled == false`) — `Encrypt`
  возвращает plaintext без префикса, чтобы инсталляции без
  криптографии продолжали работать; `Decrypt` без ключа на уже
  зашифрованном payload бросает `CryptographicException`.

**Расширение `AuditActionType`:**
- Новые значения: `LoginAttemptFailed` / `AccountLocked` /
  `AccountUnlocked` / `PasswordChanged` / `PasswordExpired` /
  `EncryptionKeyRotated` / `ConfidentialDocumentViewed` /
  `DocumentExportedToExcel` / `DocumentExportedToPdf` /
  `DocumentPrinted` / `DocumentDownloaded` / `RoleChanged` /
  `SubstitutionChanged`. Все пишутся в общий `AuditLog` с
  hash-цепочкой целостности (Phase 7).
- `AttachmentService.Download(attachmentId, downloadedById)` — отдельная
  ветка для «скачал на диск» против существующего `AttachmentViewed`
  («открыл в карточке»), пишет `DocumentDownloaded` с `EntityId =
  DocumentId` чтобы выборка «история документа» в админ-панели
  включала факт скачивания.
- `ReportService.ExportInventoryToExcel` /
  `GenerateArchiveCertificate` (DOCX→PDF) /
  `ExportRegistrationJournal` пишут `DocumentExportedToExcel` /
  `DocumentExportedToPdf` с именем выгруженного файла и количеством
  строк.

**Singleton `OrganizationSettings`:**
- Одна строка (Id=1), seed создаётся `EfDataSeeder.EnsureOrganizationSettings`.
- Поля: `EncryptionKey`, `EncryptionKeyGeneratedAt`,
  `PasswordMinLength`, `PasswordExpiryDays`, `PasswordHistoryDepth`,
  `LockoutFailureThreshold`, `LockoutWindowMinutes`,
  `LockoutDurationMinutes`. Любое поле админ-панель может изменить, но
  ключ шифрования меняется только через `IDocumentEncryptor.RotateKey`.

**Расширение `Employee`:**
- `LastPasswordChangeAt` (DateTime?) — момент последней смены пароля,
  используется для проверки 90-дневного срока. Seed выставляет
  `DateTime.UtcNow` для админа, чтобы первый вход не упирался в
  `PasswordExpired`.
- `LockedUntil` (DateTime?) — UTC-метка, до которой аккаунт заблокирован.
  `null` или `<= now` означает «разблокирован».

**Миграция `AddSecurityAndAdminPhase16`:**
- Создаёт таблицы `LoginAttempts`, `EmployeePasswordHistories`,
  `OrganizationSettings`.
- Добавляет в `Employees` колонки `LastPasswordChangeAt`, `LockedUntil`.
- FK `LoginAttempts.EmployeeId → Employees(Id)` и
  `EmployeePasswordHistories.EmployeeId → Employees(Id)` с `ON DELETE
  CASCADE` (история и попытки уезжают вместе с сотрудником).

**RBAC:** админ-панель доступна только `Admin`. Остальные роли видят
свой список последних входов (без чужих) — это будет реализовано в
follow-up вместе с UI.

**Что вынесено в follow-up:**
- `AdminViewModel` + `AdminView` (WPF UI с вкладками «Пользователи»,
  «Журнал входов», «Журнал аудита», «Организация»).
- Хук в `DocumentService` на `Encrypt`/`Decrypt` `Document.Summary` при
  `AccessLevel = Confidential` (инфраструктура `AesDocumentEncryptor`
  уже готова и покрыта тестами).
- Дополнительные audit-хуки на `DocumentPrinted`,
  `ConfidentialDocumentViewed`, `RoleChanged`, `SubstitutionChanged`
  (точки вызова в `DocumentService` / `EmployeeService` /
  `SubstitutionService`). Сами `AuditActionType`-значения уже добавлены
  и доступны для использования.

---

## Бизнес-инварианты (проверены тестами)

### Архив
- `ArchiveService.CreateRequest(...)` создаёт социально-правовой, тематический,
  платный тематический запрос или запрос копии муниципального правового акта.
- Регламентные сроки МКУ «АХУ» БМР: **30 рабочих дней** для справок/выписок,
  **15 рабочих дней** для копий муниципальных правовых актов, **7 рабочих
  дней** на перенаправление непрофильного заявления.
- `ArchiveRequest.CanCompleteRequest()` требует скан-копии паспорта и трудовой
  книжки **только** для социально-правовых запросов.
- `CompleteRequest` бросает `InvalidOperationException`, если предусловия не
  соблюдены.

### Транспорт
- `FleetService.BookVehicle(vehicleId, documentId, start, end, driverName)`
  создаёт путевой лист только при заполненных `documentId` и `driverName`.
- Бросает `VehicleBookingException`, если ТС в `Maintenance`, `end <= start`
  или интервал пересекается с существующей поездкой по Allen-алгоритму
  (полуоткрытые `[start, end)` — стыковка не считается пересечением).

### Склад / ТМЦ
- `InventoryService.ProcessTransaction(itemId, quantityChange, documentId?, userId)`
  атомарно обновляет `TotalQuantity` и пишет движение.
- Списание (`quantityChange < 0`) обязательно требует `documentId` и не
  допускает овердрафт. Любое движение всегда привязано к инициатору
  (`InitiatorId`).
- Базовая трассировка «движение → документ-основание» через
  `BasisDocumentId` (Phase 7): связывает хозяйственную операцию с приказом /
  служебной запиской.

### Регистрация документов
- `NomenclatureService.Register(documentId, caseId)` присваивает номер по
  шаблону `{ShortCode}-{CaseIndex}/{Year}-{Sequence:00000}`. При отсутствии
  дела в `caseId` подставляется `Б/Н` вместо `00`.
- В `Document` запрещены строки `RegistrationNumber`, содержащие плейсхолдеры
  `{` / `}` (валидация в `EfDocumentRepository.Save`).

### Подписи и блокировка
- `SignatureService.Sign(..., kind=Simple)` — **не** меняет `Document.IsLocked`.
- `SignatureService.Sign(..., kind=Qualified)` — взводит `IsLocked = true` и
  пишет `AuditActionType.DocumentLocked` в журнал аудита.
- На заблокированном документе разрешено менять только `Status`, исполнителя
  (`AssignedEmployeeId`) и гриф доступа (`AccessLevel`); попытка изменить
  заголовок ловится `DocumentLockGuard` и бросает исключение.
- Снимать блокировку могут `Admin` / `Manager` через `SignatureService.Unlock`,
  событие пишется в аудит.

### Согласование
- `ApprovalService.Start(...)` собирает маршрут по
  `ApprovalRouteTemplate`/`ApprovalStage` и создаёт `DocumentApproval`-стадии.
- `Approve` / `Reject` корректно учитывают активные `Substitution`-замещения:
  если штатный согласующий замещается, задание уходит замещающему.

### Поиск
- `SearchIndexService.IndexOutdated()` догоняет новые/обновлённые вложения,
  извлекает текст через `PdfTextExtractor` / `DocxTextExtractor` /
  `PlainTextExtractor` и кладёт в `AttachmentTextIndex.ExtractedText`.
- Удаление `Document` / `DocumentAttachment` каскадно удаляет индексные строки.

### Уведомления
- `INotificationService` доставляет события через in-app и опционально e-mail
  (`IEmailGateway`). По умолчанию в DI зарегистрирован `NoOpEmailGateway`;
  реальный SMTP включается через `App.config`.
- `TickReminders(now)` в `DispatcherTimer` создаёт `TaskDeadlineSoon` /
  `TaskOverdue`-напоминания **с дедупликацией на сотрудника-получателя**.

### Замещения
- `Substitution` (`OriginalEmployeeId` → `SubstituteEmployeeId`,
  `Scope: Tasks/Approvals/All`, окно `Starts/Ends`).
- `SubstitutionService.ResolveActor(employeeId, scope, when)` подменяет
  адресата задачи или согласующего на замещающего.

---

## Быстрый старт

### Сборка и тесты

```bash
dotnet restore AhuErp.sln
dotnet build   AhuErp.sln -c Release
dotnet test    AhuErp.sln --no-build -c Release
dotnet format  AhuErp.sln --verify-no-changes --exclude src/AhuErp.Core/Migrations
```

Ожидаемый результат: **0 errors, 0 warnings, 235 / 235 passed** на `main`.
В проектах `AhuErp.Core` / `AhuErp.UI` включён `TreatWarningsAsErrors`, в
`AhuErp.Tests` — нет (xunit-аналитики дают советы, не упирающиеся в баг).

### Запуск WPF приложения

WPF-приложение рассчитано на **Windows + .NET Framework 4.8** (Mono на Linux
не реализует `PresentationFramework`, поэтому `AhuErp.UI.exe` запускается
только под Windows). На Windows-машине достаточно:

```powershell
dotnet run --project src\AhuErp.UI\AhuErp.UI.csproj
```

или открыть `AhuErp.sln` в Visual Studio 2022, выбрать
`Set as Startup Project = AhuErp.UI` и нажать F5.

### Логин по умолчанию

При первом запуске на чистой БД `EfDataSeeder.EnsureSeeded` создаёт одного
администратора и наполняет справочники Phase 7 (отделы, виды документов,
номенклатура дел):

| Поле   | Значение                    |
|--------|-----------------------------|
| ФИО    | `Администратор МКУ АХУ БМР` |
| Пароль | `password`                  |
| Роль   | `Admin`                     |

Если в `Employees` уже есть записи (например, после `scripts/create-db.sql`),
но ни у кого нет `PasswordHash` — сидер обновит существующего
«Администратор МКУ АХУ БМР» (или создаст нового), чтобы вход был возможен.

> Дополнительные демо-сотрудники с ролями `Manager`, `Archivist`,
> `TechSupport`, `WarehouseManager` (пароль везде `password`) сидируются
> через `DemoDataSeeder` — он не вызывается автоматически из `App.xaml.cs`,
> а используется в xUnit-тестах и сценариях ручной проверки.

---

## Подключение к SQL Server

Контекст ищет connection string `AhuErpDb`. По умолчанию `App.config`
указывает на `(localdb)\MSSQLLocalDB`:

```xml
<add name="AhuErpDb"
     providerName="System.Data.SqlClient"
     connectionString="Server=(localdb)\MSSQLLocalDB;Database=AhuErpDb;Integrated Security=true;MultipleActiveResultSets=True;" />
```

Под свой стенд правьте `src/AhuErp.UI/App.config`, например:

```xml
<add name="AhuErpDb"
     providerName="System.Data.SqlClient"
     connectionString="Server=DESKTOP-PC\SQLEXPRESS;Database=AhuErpDb;Integrated Security=true;MultipleActiveResultSets=True;" />
```

### Создание схемы

Есть три способа поднять БД.

**Вариант 1 — `Update-Database` в Package Manager Console (Visual Studio).**

```powershell
# PMC: Default project = AhuErp.Core, StartUp project = AhuErp.UI
Update-Database -Verbose
```

**Вариант 2 — `migrate.exe` без Visual Studio.** `migrate.exe` поставляется в
NuGet-пакете `EntityFramework` в папке `tools`:

```powershell
cp $env:USERPROFILE\.nuget\packages\entityframework\6.4.4\tools\migrate.exe `
   src\AhuErp.Core\bin\Release\
cd src\AhuErp.Core\bin\Release
.\migrate.exe AhuErp.Core.dll /connectionStringName="AhuErpDb" `
              /startUpConfigurationFile="..\..\..\..\AhuErp.UI\App.config" /verbose
```

**Вариант 3 — `scripts/create-db.sql`.** Готовый T-SQL, разворачивающий схему
последней миграции одним прогоном; удобно для CI/CD и SSMS.

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d master -i scripts\create-db.sql
```

### Регенерация миграций в Linux / CI

Вспомогательный проект `tools/MigrationGenerator` позволяет скаффолдить EF6
миграции в среде без Visual Studio (в том числе в Linux через Mono):

```bash
dotnet build tools/MigrationGenerator/MigrationGenerator.csproj
mono tools/MigrationGenerator/bin/Debug/MigrationGenerator.exe \
     src/AhuErp.Core/Migrations <MigrationName>
```

Под Windows есть готовый bat-скрипт `tools/regen-migrations.bat`,
автоматически пересобирающий проект и встраивающий свежий `.resx`-снимок.

---

## Структура хранилища: ключевые таблицы

```
Documents (TPH)
  ├─ DocumentDiscriminator = "Document"        ← обычный документ
  ├─ DocumentDiscriminator = "ArchiveRequest"  ← запрос в архив (Phase 1)
  └─ DocumentDiscriminator = "ItTicket"        ← IT-заявка (Phase 3)

DocumentAttachments         (Phase 7, версионируемые вложения)
DocumentResolutions         (Phase 7, резолюции руководителя)
DocumentTasks               (Phase 7, поручения с deadline + ParentTaskId)
DocumentApprovals           (Phase 7, стадии маршрута согласования)
DocumentSignatures          (Phase 8, простая / квалифицированная)
DocumentCaseLinks           (Phase 7, привязка к делам номенклатуры)

NomenclatureCases           ← дела по номенклатуре, индекс / срок хранения
DocumentTypeRefs            ← виды документов + шаблон рег. номера
Departments                 ← иерархия отделов (Phase 7 + Phase 11)

InventoryItems              ← позиции ТМЦ (Name, Category, Unit, MinimumBalance)
InventoryTransactions       ← движение, FK на Document + Initiator + BasisDocument
Vehicles / VehicleTrips     ← парк и поездки

Substitutions               ← замещения сотрудников (Phase 11)
TaskDelegations             ← делегирование конкретного поручения (Phase 11)

Notifications               ← in-app уведомления (Phase 9)
NotificationPreferences     ← пользовательские настройки каналов

AttachmentTextIndices       ← полнотекстовый индекс (Phase 10)
SavedSearches               ← сохранённые фильтры поиска

AuditLogs                   ← хэш-цепочка событий (Phase 7)
```

---

## Roadmap

В работе (открытые PR в `main`):

- **Bug #2 — уведомления исчезают после «Прочитано».** `MyDesktopViewModel`
  получает чекбокс «Только непрочитанные», `MarkRead` теперь убирает
  уведомление из коллекции вместо тяжёлого `Reload()`, `IMessenger`
  публикует `UnreadCountChangedMessage` для обновления бейджа в шапке.
  PR [coappo/AhuErp#1](https://github.com/coappo/AhuErp/pull/1).
- **Bug #3 — новая РКК не должна быть locked-by-signature.** Баннер блокировки
  показывается только при наличии хотя бы одной `Qualified`-подписи,
  `RkkViewModel.New()` явно создаёт `Document { IsLocked = false, Status =
  Draft, ApprovalStatus = Draft, AccessLevel = Public }`, а
  `SignatureService.Sign` блокирует только Qualified-подпись. PR
  [coappo/AhuErp#2](https://github.com/coappo/AhuErp/pull/2).
- **Bug #4 — отделение резолюций от поручений.** На вкладке «3. Поручения и
  контроль» теперь две секции: «Резолюции руководителя» (`DocumentResolution`,
  `RolePolicy.CanIssueResolution`) и «Поручения по документу»
  (`DocumentTask`). При создании резолюции `ITaskService.IssueResolution`
  пишет `AuditLog` и шлёт уведомление упомянутому через `@ФамилияИО`
  исполнителю. PR [coappo/AhuErp#3](https://github.com/coappo/AhuErp/pull/3).
- **Bug #5 — склад: имена вместо Id + фильтры + цвета.** В колонках
  «Документ» и «Инициатор» отображаются нормальные строки (тип + название
  документа, ФИО сотрудника), добавлены три фильтра (по позиции, инициатору,
  периоду) и колонка «Тип операции» с цветовой подсветкой (приход —
  зелёный, расход — красный). PR
  [coappo/AhuErp#4](https://github.com/coappo/AhuErp/pull/4) (244 / 244 теста).

В планах (по приёмочному списку):

- Bug #6 + Improvement #9 — RBAC-рефакторинг (явные permission-ключи вместо
  module-ключей в `RolePolicy`).
- Bug #7 — унификация РКК (единая 6-вкладочная карточка для всех типов
  документов с условной видимостью полей).
- Bug #8 + Improvement #17 — административная панель и парольная политика
  (срок жизни пароля, история, lockout после 5 неудачных входов).
- Improvement #10 — расширение функциональности ИТО (status-pipeline по
  заявке, журнал ВКС, справочник `NetworkSegment`).
- Improvement #11 — расширенные `DocumentStatus` (`Registered`, `OnApproval`,
  `Approved`, `Rejected`, `OnSigning`, `Signed`, `OnExecution`, `Completed`,
  `Cancelled`, `Archived`) + state machine.
- Improvement #12 — журналы регистрации (входящие, исходящие, внутренние,
  договоры, ГСМ, инструктажи ОТ/ПБ, инвентаризации).
- Improvement #13 — закупки 44-ФЗ (`ProcurementPlan`, `ProcurementProcedure`,
  `Contract`, `ContractMilestone`).
- Improvement #14 — транспорт: путёвки и ГСМ (формы №3 / №4-С, расчёт
  расхода, уведомления о ОСАГО / ТО).
- Improvement #15 — эксплуатация зданий (`Building`, `Room`, `MaintenanceRequest`,
  `Inventarization`, `FixedAsset`).
- Improvement #16 — архив: внутренний workflow «передача дела в архив»,
  сроки хранения, акты уничтожения.
- Improvement #18 — DevEx: GitHub Actions CI, `Coverlet`-покрытие, `Serilog`,
  локализация в `.resx`.

---

## Авторство

Заказчик — **МКУ «АХУ» БМР** (муниципальное казённое учреждение
административно-хозяйственного управления Балаковского муниципального
района). Профильная деятельность: МТО органов МСУ, транспорт, эксплуатация
зданий, архив, делопроизводство, ИТ-сопровождение.

Подразделения: Отдел делопроизводства, Отдел хозяйственного обслуживания,
Отдел по информационно-техническому обеспечению (ИТО), Общий отдел,
Архивный отдел, Отдел учёта/отчётности/кадров, Заместитель руководителя,
Руководитель.
