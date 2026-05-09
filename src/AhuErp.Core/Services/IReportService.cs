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
    }
}
