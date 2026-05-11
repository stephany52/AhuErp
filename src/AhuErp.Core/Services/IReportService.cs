using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис выгрузки табличных отчётов и формальных документов. Абстрагирует
    /// ViewModel от ClosedXML / OpenXML, чтобы UI оставался свободным от
    /// зависимостей на форматы файлов.
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// Формирует XLSX-файл со списком всех позиций ТМЦ (ID, Наименование,
        /// Категория, Остаток) с отформатированной шапкой и автошириной колонок.
        /// </summary>
        /// <param name="filePath">Целевой путь для записи файла.</param>
        void ExportInventoryToExcel(string filePath);

        /// <summary>
        /// Генерирует Word-справку (DOCX) по архивному запросу: подставляет
        /// номер, дату, тему, статусы сканов и формальный текст ответа.
        /// </summary>
        /// <param name="archiveRequestId">Идентификатор <see cref="Models.ArchiveRequest"/>.</param>
        /// <param name="filePath">Целевой путь для записи файла.</param>
        void GenerateArchiveCertificate(int archiveRequestId, string filePath);

        /// <summary>
        /// Журнал регистрации документов за период с группировкой по дате
        /// и шапкой по требованиям делопроизводства (Рег. №, Дата, Вид,
        /// Заголовок, Корреспондент, Исполнитель, Срок, Статус).
        /// </summary>
        /// <param name="documents">Снимок документов журнала (из <see cref="IDocumentRepository.Search"/>).</param>
        /// <param name="title">Название журнала, например «Журнал входящих».</param>
        /// <param name="filePath">Целевой путь.</param>
        void ExportRegistrationJournal(IEnumerable<Document> documents, string title, string filePath);

        /// <summary>
        /// Отчёт «Исполнительская дисциплина» за период: по сотрудникам всего
        /// поручений / выполнено / просрочено / процент дисциплины.
        /// </summary>
        void ExportExecutionDisciplineReport(DateTime from, DateTime to, string filePath);

        /// <summary>
        /// Отчёт «Объём документооборота»: число документов по направлениям
        /// и видам за указанный период. По строкам — вид документа,
        /// по колонкам — направление.
        /// </summary>
        void ExportDocumentVolumeReport(DateTime from, DateTime to, string filePath);

        /// <summary>
        /// Отчёт «Просроченные поручения»: список всех активных просроченных
        /// поручений с автором, исполнителем, сроком и днями просрочки.
        /// </summary>
        void ExportOverdueTasksReport(string filePath);

        /// <summary>
        /// Отчёт «Аналитика по номенклатуре дел»: количество документов
        /// в каждом деле за период, с указанием срока хранения и отдела.
        /// </summary>
        void ExportNomenclatureAnalyticsReport(DateTime from, DateTime to, string filePath);

        // ------------------------------------------------------------
        // Phase 12 — пакет регламентированных отчётов СЭД
        // ------------------------------------------------------------

        /// <summary>
        /// Реестр отправки исходящих за период (XLSX). Включает рег. номер,
        /// дату, тему, корреспондента, способ отправки.
        /// </summary>
        void ExportOutgoingDispatchRegistry(DateTime from, DateTime to, string filePath);

        /// <summary>
        /// Опись дела: формальный DOCX по перечню документов, помещённых в
        /// номенклатурное дело (приложение № 10 Правил делопроизводства).
        /// </summary>
        void GenerateCaseInventory(int nomenclatureCaseId, string filePath);

        /// <summary>
        /// Отчёт по парку: пробег, простой, заявки за период (XLSX).
        /// </summary>
        void ExportFleetReport(DateTime from, DateTime to, string filePath);

        /// <summary>
        /// Отчёт по складу: остатки на начало/конец периода, оборот
        /// (приход/расход) за период (XLSX).
        /// </summary>
        void ExportInventoryTurnoverReport(DateTime from, DateTime to, string filePath);

        /// <summary>
        /// Полная история событий документа (PDF). Включает все записи
        /// журнала аудита для этого <see cref="Models.Document"/>:
        /// действие, актор, время, детали, хэш цепочки.
        /// </summary>
        void ExportDocumentAuditTrail(int documentId, string filePath);

        // ------------------------------------------------------------
        // Phase 15 / Improvement #12 — журналы регистрации (44-ФЗ + Устав).
        // ------------------------------------------------------------

        /// <summary>
        /// Журнал учёта ГСМ за период (XLSX). Колонки: дата, ТС, водитель,
        /// маршрут, одометр старт/финиш, пробег, выдано (л), расход (л),
        /// топливо, документ-основание.
        /// </summary>
        void ExportFuelLog(IEnumerable<Models.VehicleTrip> trips, System.DateTime from, System.DateTime to, string filePath);

        /// <summary>
        /// Журнал инструктажей по охране труда / пожарной безопасности (XLSX).
        /// Колонки: дата, вид инструктажа, тема, инструктируемый, инструктор,
        /// подпись, примечания.
        /// </summary>
        void ExportSafetyBriefingsJournal(IEnumerable<Models.SafetyBriefing> briefings, string filePath);

        /// <summary>
        /// Журнал инвентаризаций (XLSX). Колонки: дата начала/окончания, объект,
        /// председатель, состав комиссии, число расхождений, документ-акт, заметки.
        /// </summary>
        void ExportInventarizationsJournal(IEnumerable<Models.Inventarization> inventarizations, string filePath);

        /// <summary>
        /// Журнал передачи дел в архив (XLSX). Колонки: дата, дело,
        /// архивный шифр, передал, принял, акт, срок хранения, заметки.
        /// </summary>
        void ExportArchiveTransferJournal(IEnumerable<Models.ArchiveTransfer> transfers, string filePath);

        /// <summary>
        /// Журнал договоров за период (XLSX). Использует общий формат
        /// регистрационного журнала, но с заголовком «Журнал договоров».
        /// </summary>
        void ExportContractsJournal(IEnumerable<Models.Document> contracts, System.DateTime from, System.DateTime to, string filePath);

        // ------------------------------------------------------------
        // Phase 17 / Improvement #14 — печать путевого листа.
        // ------------------------------------------------------------

        /// <summary>
        /// Генерирует Word-форму путевого листа (DOCX) для конкретной поездки.
        /// Шапка и блок «работа автомобиля» подбираются по
        /// <see cref="Models.Vehicle.VehicleClass"/>:
        /// <list type="bullet">
        ///   <item><description><see cref="Models.VehicleClass.Passenger"/> —
        ///     форма №3 (Постановление Госкомстата от 28.11.1997 №78).</description></item>
        ///   <item><description><see cref="Models.VehicleClass.Truck"/> —
        ///     форма №4-С (сдельная) для повременной/сдельной перевозки.</description></item>
        ///   <item><description>остальные классы — обобщённая шапка «Путевой лист».</description></item>
        /// </list>
        /// Подставляются: ФИО водителя, гос. номер / марка / VIN, маршрут,
        /// одометр, расход топлива, фактическое время и пассажиры.
        /// </summary>
        /// <param name="tripId">Идентификатор <see cref="Models.VehicleTrip"/>.</param>
        /// <param name="filePath">Путь, по которому будет записан DOCX.</param>
        void GenerateTripWaybill(int tripId, string filePath);

        // ------------------------------------------------------------
        // Phase 19 / Improvement #16 — архив и долговременное хранение.
        // ------------------------------------------------------------

        /// <summary>
        /// Генерирует Word-форму акта о выделении к уничтожению (DOCX) по
        /// конкретному <see cref="Models.DestructionAct"/>. Структура соответствует
        /// приложению № 21 к Правилам организации хранения, комплектования,
        /// учёта и использования документов Архивного фонда РФ (Приказ
        /// Минкультуры от 31.03.2015 № 526) и Приказу Росархива от 20.12.2019 № 236.
        /// В таблицу попадают: индекс дела, заголовок, годы, срок хранения,
        /// количество документов, статья по перечню типовых документов.
        /// </summary>
        /// <param name="actId">Идентификатор <see cref="Models.DestructionAct"/>.</param>
        /// <param name="filePath">Путь, по которому будет записан DOCX.</param>
        void GenerateDestructionAct(int actId, string filePath);

        /// <summary>
        /// Генерирует Word-ответ архива (DOCX) на основании заполненного
        /// <see cref="Models.ArchiveRequest"/>:
        /// <list type="bullet">
        ///   <item><description><see cref="Models.ArchiveResponseKind.Spravka"/> —
        ///     архивная справка (официальный документ с собственным текстом).</description></item>
        ///   <item><description><see cref="Models.ArchiveResponseKind.Vypiska"/> —
        ///     архивная выписка (дословное извлечение).</description></item>
        ///   <item><description><see cref="Models.ArchiveResponseKind.Kopiya"/> —
        ///     архивная копия (заверение копии документа).</description></item>
        /// </list>
        /// </summary>
        /// <param name="archiveRequestId">Идентификатор <see cref="Models.ArchiveRequest"/>.</param>
        /// <param name="kind">Тип ответа архива.</param>
        /// <param name="filePath">Путь, по которому будет записан DOCX.</param>
        void GenerateArchiveResponse(int archiveRequestId, Models.ArchiveResponseKind kind, string filePath);
    }
}
