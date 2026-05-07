# AhuErp — ERP/EDMS для МКУ АХУ БМР

Phase 1 (Foundation) комплексной системы управления документооборотом и административно-хозяйственной деятельностью. Заложен архитектурный фундамент: строгий MVVM, чистое разделение доменной модели, EF6 Code-First и покрытие бизнес-логики unit-тестами.

## Архитектура

```
AhuErp.sln
├── src/
│   ├── AhuErp.Core/     ← .NET Framework 4.8, SDK-style class library
│   │   ├── Models/      ← Employee, Document, ArchiveRequest (TPH), Vehicle, VehicleTrip
│   │   ├── Data/        ← AhuDbContext (EF6)
│   │   ├── Migrations/  ← EF6 Code-First миграции (+ .resx snapshot)
│   │   └── Services/    ← ArchiveService, FleetService, DashboardService
│   └── AhuErp.UI/       ← .NET Framework 4.8, SDK-style WPF Application (UseWPF)
│       ├── App.xaml/.cs
│       ├── MainWindow.xaml/.cs   ← только InitializeComponent()
│       ├── ViewModels/  ← MVVM на CommunityToolkit.Mvvm
│       ├── Views/       ← DashboardView, PlaceholderView
│       └── Converters/  ← OverdueRowColorConverter
└── tests/
    └── AhuErp.Tests/    ← xUnit, сервисные и интеграционные тесты
```

Все три проекта — SDK-style `.csproj`, `TargetFramework=net48`, что позволяет
`dotnet build` / `dotnet test` / `dotnet format` работать без Visual Studio
(в том числе на CI под Linux через `Microsoft.NETFramework.ReferenceAssemblies`).

## Бизнес-логика (Phase 1)

### Архив
- `ArchiveService.CreateRequest(...)` создаёт социально-правовой, тематический или платный тематический запрос, либо запрос копии муниципального правового акта.
- Регламентные сроки МКУ «АХУ» БМР: 30 рабочих дней для справок/выписок, 15 рабочих дней для копий муниципальных правовых актов, 7 рабочих дней на перенаправление непрофильного заявления.
- `ArchiveRequest.CanCompleteRequest()` требует скан-копии паспорта и трудовой книжки только для социально-правовых запросов.
- `ArchiveService.CompleteRequest(...)` бросает `InvalidOperationException`, если предварительные условия не соблюдены.

### Автопарк
- `FleetService.BookVehicle(vehicle, start, end, existingTrips?)` создаёт путевой лист.
- Бросает `VehicleBookingException`, если:
  - ТС в статусе `Maintenance`;
  - интервал пересекается с уже существующей поездкой (`Allen-overlap`);
  - `end <= start`.

### Дашборд
- `DashboardService.CountOverdue(docs, now)` — документы с `Deadline < now` и `Status ∉ {Completed, Cancelled}`.
- `DashboardService.CountDueSoon(docs, now, daysThreshold=3)` — активные документы с дедлайном в ближайшие N суток.

### UI / MVVM
- `MainViewModel` держит `ObservableCollection<NavigationItem>` и свойство `CurrentViewModel`.
- Сайдбар: «Дашборд / Документационное обеспечение / Архивный отдел / Склад / ТМЦ / ИТО / Транспорт», каждая кнопка через `RelayCommand` переключает `SelectedNavigationItem`.
- `OverdueRowColorConverter` — `IValueConverter`, возвращающий `Red` / `Yellow` / `Transparent` в зависимости от `Document.Deadline` и `Status`.

## Быстрый старт

### Сборка и тесты

```bash
dotnet restore AhuErp.sln
dotnet build   AhuErp.sln -c Debug
dotnet test    AhuErp.sln -c Debug
dotnet format  AhuErp.sln --verify-no-changes --exclude src/AhuErp.Core/Migrations
```

Ожидаемый результат: **0 errors, 0 warnings, все xUnit-тесты passed**.

### Запуск WPF приложения

WPF-приложение рассчитано на Windows (net48). На Windows-машине достаточно:

```powershell
dotnet run --project src\AhuErp.UI\AhuErp.UI.csproj
```

### Логин по умолчанию (Phase 6, EF6-бэкенд)

При первом запуске на чистой БД `EfDataSeeder` создаёт одного администратора:

| Поле        | Значение                  |
|-------------|---------------------------|
| ФИО         | `Администратор МКУ АХУ БМР` |
| Пароль      | `password`                |
| Роль        | `Admin`                   |

Если в БД уже есть строки в `Employees` (например, был раскомментирован сид-блок
в `scripts/create-db.sql`), но ни у кого нет `PasswordHash` — сидер обновит
существующего «Администратор МКУ АХУ БМР» (или создаст нового), чтобы вход был возможен.

## Phase 6: переключение DI с in-memory на EF6

Начиная с Phase 6 все четыре репозитория зарегистрированы как EF6-реализации
поверх `AhuDbContext` (`EfDocumentRepository`, `EfEmployeeRepository`,
`EfInventoryRepository`, `EfVehicleRepository`). Контекст — singleton, обращения
с UI-потока, тесты остаются на in-memory (быстрые, без SQL Server).

Шаги для запуска на своей машине:

1. Установить SQL Server Express (или подключить LocalDB / удалённый сервер).
2. Накатить схему скриптом `scripts/create-db.sql` (SSMS → File → Open → F5).
3. Поправить `src/AhuErp.UI/App.config` под свой инстанс, например:
   ```xml
   <add name="AhuErpDb"
        providerName="System.Data.SqlClient"
        connectionString="Server=DESKTOP-PC\SQLEXPRESS;Database=AhuErpDb;Integrated Security=true;MultipleActiveResultSets=True;" />
   ```
4. Собрать и запустить (`Set as Startup Project = AhuErp.UI`, F5).

## Применение EF6 миграции к SQL Server

Миграция `20260423121238_InitialCreate` разворачивает полную схему первой фазы.

### Вариант 1. Через `Update-Database` в Package Manager Console (Visual Studio)

1. Убедитесь, что `App.config` в `AhuErp.UI` (или свой config под ваш стенд) содержит connection string `AhuErpDb`. По умолчанию задано:
   ```
   Server=(localdb)\MSSQLLocalDB;Database=AhuErpDb;Integrated Security=true
   ```
2. В PMC выберите Default project = `AhuErp.Core`, StartUp project = `AhuErp.UI`.
3. Выполните:
   ```powershell
   Update-Database -Verbose
   ```

### Вариант 2. Через `migrate.exe` (без Visual Studio)

`migrate.exe` поставляется в NuGet-пакете `EntityFramework` в папке `tools`. После `dotnet build`:

```powershell
# Windows
cp $env:USERPROFILE\.nuget\packages\entityframework\6.4.4\tools\migrate.exe `
   src\AhuErp.Core\bin\Debug\
cd src\AhuErp.Core\bin\Debug
.\migrate.exe AhuErp.Core.dll /connectionStringName="AhuErpDb" /startUpConfigurationFile="..\..\..\AhuErp.UI\App.config" /verbose
```

### Вариант 3. Сгенерировать идемпотентный T-SQL скрипт

```powershell
Update-Database -Script -SourceMigration $InitialDatabase -TargetMigration InitialCreate -Verbose
```

Полученный `.sql` можно применить через `sqlcmd`, SSMS или любой CI/CD-пайплайн.

## Регенерация миграции в Linux / CI

Вспомогательный проект `tools/MigrationGenerator` позволяет скаффолдить EF6
миграции в среде без Visual Studio (в том числе в Linux через `mono`):

```bash
dotnet build tools/MigrationGenerator/MigrationGenerator.csproj
mono tools/MigrationGenerator/bin/Debug/MigrationGenerator.exe \
     src/AhuErp.Core/Migrations InitialCreate
```

## Стек

- .NET Framework 4.8 (SDK-style `.csproj`)
- WPF + MVVM (`CommunityToolkit.Mvvm` 8.3)
- Entity Framework 6.4.4 (Code-First + Migrations)
- xUnit 2.9
- Ref. assemblies: `Microsoft.NETFramework.ReferenceAssemblies 1.0.3`

## Phase 2 — DI, Authentication & Office/Archive CRUD

- Добавлен DI-контейнер `Microsoft.Extensions.DependencyInjection` в `App.xaml.cs`, регистрирующий сервисы (`IAuthService`, `IPasswordHasher`, репозитории) и все ViewModel-и.
- `EmployeeRole` (Admin / Manager / Archivist / TechSupport / WarehouseManager) и `PasswordHash` добавлены к `Employee`. Миграция `AddEmployeeAuth` (`20260423125626`) добавляет соответствующие колонки.
- `IAuthService`/`AuthService` + PBKDF2-`Pbkdf2PasswordHasher` с константным сравнением. `LoginWindow` показывается первым при старте приложения; `MainWindow` открывается только после успешной аутентификации.
- RBAC: `RolePolicy` — декларативная таблица «роль → доступные модули»; `MainViewModel` фильтрует `NavigationItems` по текущему пользователю, `BooleanToVisibilityConverter` скрывает недоступные пункты меню.
- CRUD экраны «Отдел документационного обеспечения» (Incoming/Internal документы, `OfficeView`) и «Архивный отдел» (`ArchiveRequest` со скан-чекбоксами и действием «Завершить», `ArchiveView`) — работают поверх `IDocumentRepository` (in-memory на Phase 2, EF6 на Phase 3+).
- Демо-пользователи (пароль `password`): «Администратор МКУ АХУ БМР» (Admin), «Стерликов Дмитрий Николаевич» (Manager), «Бурдина Галина Николаевна» (Archivist), «Дорофеев Артем Валерьевич» (TechSupport), «Зайченко Татьяна Александровна» (WarehouseManager).
- Тесты: **+38** — `AuthServiceTests`, `PasswordHasherTests`, `RolePolicyTests`, `InMemoryDocumentRepositoryTests`. Итого 59 зелёных.

## Phase 3 — Warehouse / ТМЦ + IT-Service (Help Desk)

- Модели: `InventoryItem` (Id, Name, `InventoryCategory`, TotalQuantity), `InventoryTransaction` (InventoryItemId, nullable `DocumentId`, QuantityChanged ±, TransactionDate, InitiatorId), `ItTicket` (наследник `Document` через TPH-дискриминатор — `AffectedEquipment`, `ResolutionNotes`).
- EF6 миграция `20260423131841_AddInventoryAndItTicket`: две новые таблицы + FK `InventoryTransactions.DocumentId → Documents`, `InitiatorId → Employees`, колонки `AffectedEquipment`/`ResolutionNotes` на `Documents` для TPH-подтипа `ItTicket`.
- `IInventoryService` / `InventoryService.ProcessTransaction(itemId, quantityChange, documentId?, userId)` — атомарно обновляет `TotalQuantity` и записывает движение. Правила: `quantityChange != 0`, списание требует `documentId`, при этом `TotalQuantity + quantityChange >= 0` (иначе `InvalidOperationException`).
- UI: `WarehouseView` — грид остатков + панель прихода/расхода (расход обязательно привязан к документу из `IDocumentRepository.ListInventoryEligibleDocuments()` — внутренние распоряжения + IT-заявки) + лента последних 20 движений.
- UI: `ItServiceView` — CRUD `ItTicket`; при закрытии заявки можно опционально списать расходник со склада — списание проходит через `IInventoryService` с `DocumentId = ticket.Id`, т.е. движение ТМЦ всегда связано с документом (IT-заявкой или приказом).
- Тесты: **+9** юнит-тестов (`InventoryServiceTests`): приход / расход / запрет овердрафта / обязательность документа при списании / нулевой/невалидный инициатор / отсутствующая позиция / граничный нулевой остаток / трассировка `DocumentId` по нескольким движениям. Итого **68/68**.

### Как реализована связка «движение ТМЦ → документ-основание»

```
InventoryTransaction.DocumentId? ──(FK, ON DELETE NO ACTION)──► Documents.Id
                                                                   │
                                                                   ├─ Document         (Incoming / Internal)
                                                                   ├─ ArchiveRequest   (TPH)
                                                                   └─ ItTicket         (TPH ← Phase 3)
```

Любое списание через `InventoryService.ProcessTransaction(..., documentId: X, ...)`:
- валидирует, что `X != null` и позиция имеет достаточный остаток,
- уменьшает `InventoryItem.TotalQuantity` на абсолютную величину,
- добавляет запись `InventoryTransaction { QuantityChanged < 0, DocumentId = X, InitiatorId = currentUser.Id, TransactionDate = now }`.

При закрытии `ItTicket` в UI `ItServiceViewModel.Resolve()` автоматически передаёт `documentId: SelectedTicket.Id`, поэтому любое списание из Help Desk прослеживается до конкретной заявки.

## Phase 4 — Fleet / Автопарк

- Модель `VehicleTrip` расширена полем `DriverName` (StringLength 128). `DocumentId` остаётся nullable на уровне БД для обратной совместимости с ранее созданными поездками, но новый API бронирования требует заполненного значения. EF6 миграция `20260423175847_AddVehicleTripDriverName` добавляет колонку.
- `IVehicleRepository` + `InMemoryVehicleRepository` — абстракция хранилища автопарка (`ListVehicles`, `GetVehicle`, `ListTrips(vehicleId)`, `AddVehicle`, `AddTrip`).
- `IFleetService` получает вторую перегрузку `BookVehicle(int vehicleId, int documentId, DateTime start, DateTime end, string driverName)`. Phase 1-перегрузка `BookVehicle(Vehicle, ...)` сохранена для обратной совместимости и тестов.
- UI: `FleetView` — три секции (список ТС → расписание выбранного ТС → форма бронирования с `DatePicker`/`TextBox`/`ComboBox` документа). Кнопка «Забронировать» ловит `VehicleBookingException` и показывает пользователю понятное сообщение без закрытия формы.
- DI: `IVehicleRepository`, `IFleetService` и `FleetViewModel` зарегистрированы в `AppServices`. `MainWindow.xaml` подключает `FleetView` вместо плейсхолдера.
- Демо-данные (`DemoDataSeeder.SeedFleet`): Lada Largus / «ГАЗель NEXT» / «УАЗ Патриот» (последний на обслуживании) + две заявки на транспорт (`DocumentType.Fleet`), привязанные к адресам МКУ «АХУ» БМР.
- Тесты: **+10** (`FleetServicePhase4Tests`) — успешное бронирование без пересечений; точное / частичное (слева и справа) / содержащее пересечение; стыковка интервалов [a,b) без пересечения; отсутствие ТС; ТС на обслуживании; пустые `driverName` / `documentId`. Итого **78 / 78**.

### LINQ-логика проверки пересечений

Пересечение интервалов ищется по классическому Allen-алгоритму:

```csharp
existingTrips.Any(t => t.VehicleId == vehicleId
                       && t.StartDate < endDate
                       && t.EndDate   > startDate);
```

Эквивалентный инвариант: «две встречи пересекаются ⇔ каждая начинается раньше конца другой». Если любое существующее бронирование удовлетворяет ему — `FleetService` выбрасывает `VehicleBookingException`. Пограничный случай `existing.EndDate == newStart` считается стыковкой (не пересечением): интервалы трактуются как полуоткрытые `[start, end)`, поэтому «спина-к-спине» бронирование разрешено.

Вычисление инкапсулировано в `VehicleTrip.OverlapsWith(start, end)` и переиспользуется из обеих перегрузок `FleetService.BookVehicle`.

## Phase 5 — Dashboard-аналитика и экспорт отчётов

- **NuGet**: `ClosedXML 0.102.3` + `DocumentFormat.OpenXml 2.20.0` (в `AhuErp.Core`) и `LiveCharts.Wpf 0.9.7` (в `AhuErp.UI`). Все три пакета совместимы с `net48` и не требуют установленного MS Office.
- **`IReportService`** (`AhuErp.Core`):
  - `ExportInventoryToExcel(filePath)` — ClosedXML, лист «Склад ТМЦ», отформатированная шапка (bold + фон LightSteelBlue + нижняя граница), колонки `№ / Наименование / Категория / Остаток`, `Columns().AdjustToContents()`.
  - `GenerateArchiveCertificate(archiveRequestId, filePath)` — DOCX через `WordprocessingDocument` + `DocumentFormat.OpenXml.Wordprocessing`. Заголовок «АРХИВНАЯ СПРАВКА», реквизиты архивного отдела, подстановка `№`, даты создания, вида запроса, темы, срока, статусов сканов паспорта/трудовой и выбор одного из двух формальных абзацев (полный пакет / требуется досбор).
- **UI**:
  - `WarehouseViewModel.ExportToExcelCommand` — `IFileDialogService.PromptSaveFile(...)` → `IReportService.ExportInventoryToExcel`. Отдельно обрабатываются `IOException` (файл занят), `UnauthorizedAccessException` и прочие `Exception`.
  - `ArchiveViewModel.GenerateCertificateCommand` — аналогичный flow для Word-справки, активна только при выбранной заявке.
  - `IFileDialogService` / `FileDialogService` — тонкая обёртка над `Microsoft.Win32.SaveFileDialog`, чтобы ViewModel оставался не зависящим от WPF-диалогов.
- **Дашборд** (`DashboardViewModel` + `DashboardView.xaml`):
  - KPI-карточки: просроченные архивные заявки, ТС в рейсе сейчас, ТМЦ с остатком < 5, всего просроченных документов, с дедлайном ≤ 3 дней.
  - `lvc:PieChart` — распределение документов по `DocumentStatus`. `lvc:CartesianChart` с `ColumnSeries` — сумма остатков ТМЦ по категориям.
  - Данные собираются в `Task.Run(...)` → `ConfigureAwait(true)`, UI-поток не блокируется. Повторная загрузка доступна через `RefreshCommand`. `IsLoading` гасит кнопку и показывает индикатор «Загрузка…».
- **DI** (`AppServices.ConfigureServices`): `IReportService → ReportService` (singleton) и `IFileDialogService → FileDialogService` добавлены рядом с другими сервисами; `DashboardViewModel` уже был зарегистрирован и отображается первым пунктом навигации — значит, для ролей `Admin` и `Manager` он автоматически открывается при входе (см. конструктор `MainViewModel`, выбор первого `IsAllowed` элемента).
- **Тесты**: `ReportServiceTests` — `+4` теста (XLSX-шапка и строки, DOCX с полным пакетом сканов, DOCX-follow-up при отсутствии сканов, ошибка при отсутствующей заявке). XLSX открывается обратно через ClosedXML, DOCX — через `System.IO.Packaging` + `word/document.xml`, без MS Office. Итого **82 / 82** зелёных.

### LiveCharts DataContext-биндинги

`PieChart.Series` и `CartesianChart.Series` биндятся к `SeriesCollection`-свойствам `DocumentStatusSeries` и `InventoryByCategorySeries` в `DashboardViewModel`. `CartesianChart.AxisX.Labels` биндится к `string[] InventoryCategoryLabels`. `DashboardViewModel` наследует `ViewModelBase : ObservableObject` (CommunityToolkit.Mvvm), поэтому `[ObservableProperty]` генерирует `INotifyPropertyChanged`-уведомления, и LiveCharts перестраивает диаграммы при каждом `RefreshAsync()`. Важно: `SeriesCollection` собирается в фоновом `Task.Run`, но применяется к VM в UI-потоке через `await ... .ConfigureAwait(true)` — LiveCharts поддерживает только UI-поточное обновление.

## Acceptance fixes — Bug #1: автоматическая регистрация РКК

- `RkkViewModel.Save()` автоматически регистрирует сохранённую РКК через `NomenclatureService.Register(...)`, если документ ещё не зарегистрирован и `RolePolicy.AutoRegisterOnSave == true`.
- Регистрационный номер всегда формируется как `{TypeCode}-{YYYY}-{N:D5}`; шаблонные значения с `{` или `}` запрещены на уровне `Document`/репозиториев/`AhuDbContext.SaveChanges()`.
- Последовательность `N` хранится в `NomenclatureCounters` с уникальным ключом `(TypeCode, Year)`; EF-репозиторий выдаёт номер в транзакции `Serializable`, in-memory репозиторий защищён lock-ом.
- При регистрации выставляются `RegistrationDate = DateTime.Now` и `Status = DocumentStatus.Registered`.
- Покрытие: `NomenclatureServiceTests.Register_AssignsSequentialNumber`, `Register_DoesNotReuseNumberAfterDelete`, `Register_IsAtomicUnderConcurrency`, плюс проверка запрета шаблонных номеров.

## Roadmap (будущие итерации)

- Аудит-лог и отчётность (кто/что/когда), а также полная миграция in-memory репозиториев на реальный `AhuDbContext`/EF6 (сейчас DI-слой готов к подмене без изменений ViewModel).
