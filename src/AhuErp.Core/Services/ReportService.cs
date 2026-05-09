using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Reports;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IReportService"/>. Использует ClosedXML для XLSX
    /// и DocumentFormat.OpenXml для DOCX — оба работают на чистом .NET без
    /// установленного MS Office.
    /// </summary>
    public sealed class ReportService : IReportService
    {
        private readonly IInventoryRepository _inventory;
        private readonly IDocumentRepository _documents;
        private readonly ITaskService _tasks;
        private readonly ITaskRepository _taskRepo;
        private readonly INomenclatureRepository _nomenclature;
        private readonly IVehicleRepository _vehicles;
        private readonly IAuditService _audit;

        public ReportService(IInventoryRepository inventory, IDocumentRepository documents)
            : this(inventory, documents, null, null, null, null, null)
        {
        }

        /// <summary>
        /// 5-аргументная перегрузка для Phase 4-9 отчётов СЭД. Phase 12
        /// расширил список зависимостей — для совместимости с уже
        /// существующими тестами эта перегрузка делегирует в полную.
        /// </summary>
        public ReportService(
            IInventoryRepository inventory,
            IDocumentRepository documents,
            ITaskService tasks,
            ITaskRepository taskRepo,
            INomenclatureRepository nomenclature)
            : this(inventory, documents, tasks, taskRepo, nomenclature, null, null)
        {
        }

        /// <summary>
        /// Phase 12 — расширенный конструктор: добавляются зависимости автопарка
        /// (для отчёта по парку) и журнала аудита (для PDF-выгрузки полной
        /// истории документа). Старые перегрузки оставлены ради совместимости.
        /// </summary>
        public ReportService(
            IInventoryRepository inventory,
            IDocumentRepository documents,
            ITaskService tasks,
            ITaskRepository taskRepo,
            INomenclatureRepository nomenclature,
            IVehicleRepository vehicles,
            IAuditService audit)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _tasks = tasks;
            _taskRepo = taskRepo;
            _nomenclature = nomenclature;
            _vehicles = vehicles;
            _audit = audit;
        }

        public void ExportInventoryToExcel(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Склад ТМЦ");

                sheet.Cell(1, 1).Value = "№";
                sheet.Cell(1, 2).Value = "Наименование";
                sheet.Cell(1, 3).Value = "Категория";
                sheet.Cell(1, 4).Value = "Остаток";

                var header = sheet.Range(1, 1, 1, 4);
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                header.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                var row = 2;
                foreach (var item in _inventory.ListItems().OrderBy(i => i.Name))
                {
                    sheet.Cell(row, 1).Value = item.Id;
                    sheet.Cell(row, 2).Value = item.Name;
                    sheet.Cell(row, 3).Value = FormatCategory(item.Category);
                    sheet.Cell(row, 4).Value = item.TotalQuantity;
                    row++;
                }

                sheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }

            _audit?.Record(AuditActionType.DocumentExportedToExcel,
                entityType: "InventoryReport", entityId: null, userId: null,
                details: System.IO.Path.GetFileName(filePath));
        }

        public void GenerateArchiveCertificate(int archiveRequestId, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            var document = _documents.GetById(archiveRequestId) as ArchiveRequest
                ?? throw new InvalidOperationException(
                    $"Архивный запрос #{archiveRequestId} не найден.");

            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new W.Document();
                var body = main.Document.AppendChild(new W.Body());

                body.AppendChild(Paragraph(OrganizationProfile.FullName));
                body.AppendChild(Paragraph(OrganizationProfile.ArchiveDepartmentName));
                body.AppendChild(Paragraph(OrganizationProfile.ArchiveAddress));
                body.AppendChild(Paragraph($"Телефон: {OrganizationProfile.ArchivePhone}; e-mail: {OrganizationProfile.ArchiveEmail}"));
                body.AppendChild(Paragraph(string.Empty));
                body.AppendChild(Heading("АРХИВНАЯ СПРАВКА"));
                body.AppendChild(Paragraph($"по архивному запросу №{document.Id} от {document.CreationDate:dd.MM.yyyy}"));
                body.AppendChild(Paragraph(string.Empty));
                body.AppendChild(Paragraph($"Вид запроса: {FormatArchiveRequestKind(document.RequestKind)}"));
                body.AppendChild(Paragraph($"Тема запроса: {document.Title}"));
                body.AppendChild(Paragraph($"Срок исполнения: {document.Deadline:dd.MM.yyyy}"));
                body.AppendChild(Paragraph(string.Empty));

                var passport = document.HasPassportScan ? "приложен" : "не приложен";
                var workBook = document.HasWorkBookScan ? "приложена" : "не приложена";
                body.AppendChild(Paragraph($"Скан паспорта: {passport}."));
                body.AppendChild(Paragraph($"Скан трудовой книжки: {workBook}."));
                body.AppendChild(Paragraph(string.Empty));

                if (document.HasPassportScan && document.HasWorkBookScan)
                {
                    body.AppendChild(Paragraph(
                        "Настоящим подтверждается, что документы представлены в полном объёме. " +
                        "Архивная справка, выписка или копия подготовлена для выдачи заявителю."));
                }
                else
                {
                    body.AppendChild(Paragraph(
                        "Для выдачи архивной справки необходимо дополнительно представить " +
                        "отсутствующие документы, после чего запрос будет обработан повторно."));
                }

                body.AppendChild(Paragraph(string.Empty));
                body.AppendChild(Paragraph($"Начальник архивного отдела _________________________ {OrganizationProfile.ArchiveHeadShortName}"));
                body.AppendChild(Paragraph($"Дата оформления: {DateTime.Now:dd.MM.yyyy}"));

                main.Document.Save();
            }

            _audit?.Record(AuditActionType.DocumentExportedToPdf,
                entityType: nameof(ArchiveRequest), entityId: archiveRequestId, userId: null,
                details: $"docx={System.IO.Path.GetFileName(filePath)}");
        }

        public void ExportRegistrationJournal(IEnumerable<Document> documents, string title, string filePath)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            var rows = documents.ToList();
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add(SafeSheetName(string.IsNullOrWhiteSpace(title) ? "Журнал" : title));

                sheet.Cell(1, 1).Value = string.IsNullOrWhiteSpace(title) ? "Журнал регистрации" : title;
                sheet.Range(1, 1, 1, 9).Merge().Style
                    .Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}";
                sheet.Range(2, 1, 2, 9).Merge();

                int header = 4;
                sheet.Cell(header, 1).Value = "Рег. №";
                sheet.Cell(header, 2).Value = "Дата рег.";
                sheet.Cell(header, 3).Value = "Направление";
                sheet.Cell(header, 4).Value = "Вид";
                sheet.Cell(header, 5).Value = "Заголовок";
                sheet.Cell(header, 6).Value = "Корреспондент";
                sheet.Cell(header, 7).Value = "Исполнитель";
                sheet.Cell(header, 8).Value = "Срок";
                sheet.Cell(header, 9).Value = "Статус";

                var hdr = sheet.Range(header, 1, header, 9);
                hdr.Style.Font.Bold = true;
                hdr.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hdr.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = header + 1;
                foreach (var d in rows)
                {
                    sheet.Cell(row, 1).Value = d.RegistrationNumber ?? "—";
                    sheet.Cell(row, 2).Value = d.RegistrationDate?.ToString("dd.MM.yyyy") ?? "—";
                    sheet.Cell(row, 3).Value = FormatDirection(d.Direction);
                    sheet.Cell(row, 4).Value = d.DocumentTypeRef?.Name ?? FormatDocumentType(d.Type);
                    sheet.Cell(row, 5).Value = d.Title ?? string.Empty;
                    sheet.Cell(row, 6).Value = d.Correspondent ?? string.Empty;
                    sheet.Cell(row, 7).Value = d.AssignedEmployee?.FullName ?? string.Empty;
                    sheet.Cell(row, 8).Value = d.Deadline.ToString("dd.MM.yyyy");
                    sheet.Cell(row, 9).Value = FormatDocumentStatus(d.Status);
                    row++;
                }

                sheet.Columns().AdjustToContents();
                sheet.Column(5).Width = Math.Min(60, sheet.Column(5).Width);
                workbook.SaveAs(filePath);
            }

            _audit?.Record(AuditActionType.DocumentExportedToExcel,
                entityType: "RegistrationJournal", entityId: null, userId: null,
                details: $"{title}; rows={rows.Count}");
        }

        public void ExportExecutionDisciplineReport(DateTime from, DateTime to, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));
            if (_tasks == null)
                throw new InvalidOperationException("ReportService не настроен для отчётов СЭД (нет ITaskService).");

            var report = _tasks.BuildDisciplineReport(from, to);

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Дисциплина");
                sheet.Cell(1, 1).Value = "Исполнительская дисциплина";
                sheet.Range(1, 1, 1, 6).Merge().Style.Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";
                sheet.Range(2, 1, 2, 6).Merge();

                sheet.Cell(4, 1).Value = "Всего поручений";
                sheet.Cell(4, 2).Value = report.TotalTasks;
                sheet.Cell(5, 1).Value = "В срок";
                sheet.Cell(5, 2).Value = report.CompletedOnTime;
                sheet.Cell(6, 1).Value = "С нарушением срока";
                sheet.Cell(6, 2).Value = report.CompletedLate;
                sheet.Cell(7, 1).Value = "Просрочено (открытые)";
                sheet.Cell(7, 2).Value = report.Overdue;
                sheet.Cell(8, 1).Value = "В работе";
                sheet.Cell(8, 2).Value = report.InProgress;

                int hdr = 10;
                sheet.Cell(hdr, 1).Value = "Исполнитель";
                sheet.Cell(hdr, 2).Value = "Всего";
                sheet.Cell(hdr, 3).Value = "В срок";
                sheet.Cell(hdr, 4).Value = "Опоздание";
                sheet.Cell(hdr, 5).Value = "Просрочено";
                sheet.Cell(hdr, 6).Value = "% дисциплины";
                var headerRange = sheet.Range(hdr, 1, hdr, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = hdr + 1;
                foreach (var r in report.ByExecutor)
                {
                    double pct = r.Total == 0 ? 0.0 : 100.0 * r.CompletedOnTime / r.Total;
                    sheet.Cell(row, 1).Value = r.ExecutorName;
                    sheet.Cell(row, 2).Value = r.Total;
                    sheet.Cell(row, 3).Value = r.CompletedOnTime;
                    sheet.Cell(row, 4).Value = r.CompletedLate;
                    sheet.Cell(row, 5).Value = r.Overdue;
                    sheet.Cell(row, 6).Value = Math.Round(pct, 1);
                    row++;
                }

                sheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        public void ExportDocumentVolumeReport(DateTime from, DateTime to, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            // `to` обычно приходит как полночь (DatePicker), поэтому
            // расширяем до конца дня — иначе документы за последний день
            // периода не попадут в выборку (`<= to` в Search).
            var toInclusive = ExtendToEndOfDay(to);
            var docs = _documents.Search(new DocumentSearchFilter { From = from, To = toInclusive });

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Объём");
                sheet.Cell(1, 1).Value = "Объём документооборота";
                sheet.Range(1, 1, 1, 6).Merge().Style.Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}; всего: {docs.Count}";
                sheet.Range(2, 1, 2, 6).Merge();

                int hdr = 4;
                sheet.Cell(hdr, 1).Value = "Вид документа";
                sheet.Cell(hdr, 2).Value = "Входящих";
                sheet.Cell(hdr, 3).Value = "Исходящих";
                sheet.Cell(hdr, 4).Value = "Внутренних";
                sheet.Cell(hdr, 5).Value = "Распорядительных";
                sheet.Cell(hdr, 6).Value = "Итого";
                var headerRange = sheet.Range(hdr, 1, hdr, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                var grouped = docs
                    .GroupBy(d => d.DocumentTypeRef?.Name ?? FormatDocumentType(d.Type))
                    .OrderBy(g => g.Key)
                    .ToList();

                int row = hdr + 1;
                int totalIn = 0, totalOut = 0, totalInt = 0, totalDir = 0;
                foreach (var g in grouped)
                {
                    int incoming = g.Count(d => d.Direction == DocumentDirection.Incoming);
                    int outgoing = g.Count(d => d.Direction == DocumentDirection.Outgoing);
                    int internalCnt = g.Count(d => d.Direction == DocumentDirection.Internal);
                    int directiveCnt = g.Count(d => d.Direction == DocumentDirection.Directive);
                    sheet.Cell(row, 1).Value = g.Key;
                    sheet.Cell(row, 2).Value = incoming;
                    sheet.Cell(row, 3).Value = outgoing;
                    sheet.Cell(row, 4).Value = internalCnt;
                    sheet.Cell(row, 5).Value = directiveCnt;
                    sheet.Cell(row, 6).Value = incoming + outgoing + internalCnt + directiveCnt;
                    totalIn += incoming; totalOut += outgoing;
                    totalInt += internalCnt; totalDir += directiveCnt;
                    row++;
                }

                sheet.Cell(row, 1).Value = "Итого";
                sheet.Cell(row, 2).Value = totalIn;
                sheet.Cell(row, 3).Value = totalOut;
                sheet.Cell(row, 4).Value = totalInt;
                sheet.Cell(row, 5).Value = totalDir;
                sheet.Cell(row, 6).Value = totalIn + totalOut + totalInt + totalDir;
                sheet.Range(row, 1, row, 6).Style.Font.Bold = true;
                sheet.Range(row, 1, row, 6).Style.Border.TopBorder = XLBorderStyleValues.Medium;

                sheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        public void ExportOverdueTasksReport(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));
            if (_tasks == null)
                throw new InvalidOperationException("ReportService не настроен для отчётов СЭД (нет ITaskService).");

            var now = DateTime.Now;
            var overdue = _tasks.ListOverdue(now);

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Просроченные");
                sheet.Cell(1, 1).Value = "Просроченные поручения";
                sheet.Range(1, 1, 1, 7).Merge().Style.Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Сформировано: {now:dd.MM.yyyy HH:mm}; всего: {overdue.Count}";
                sheet.Range(2, 1, 2, 7).Merge();

                int hdr = 4;
                sheet.Cell(hdr, 1).Value = "№ поручения";
                sheet.Cell(hdr, 2).Value = "Документ";
                sheet.Cell(hdr, 3).Value = "Автор";
                sheet.Cell(hdr, 4).Value = "Исполнитель";
                sheet.Cell(hdr, 5).Value = "Срок";
                sheet.Cell(hdr, 6).Value = "Дней просрочки";
                sheet.Cell(hdr, 7).Value = "Описание";
                var headerRange = sheet.Range(hdr, 1, hdr, 7);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = hdr + 1;
                foreach (var t in overdue)
                {
                    sheet.Cell(row, 1).Value = t.Id;
                    sheet.Cell(row, 2).Value = t.Document?.RegistrationNumber ?? $"#{t.DocumentId}";
                    sheet.Cell(row, 3).Value = t.Author?.FullName ?? string.Empty;
                    sheet.Cell(row, 4).Value = t.Executor?.FullName ?? string.Empty;
                    sheet.Cell(row, 5).Value = t.Deadline.ToString("dd.MM.yyyy");
                    sheet.Cell(row, 6).Value = (int)Math.Ceiling((now - t.Deadline).TotalDays);
                    sheet.Cell(row, 7).Value = t.Description ?? string.Empty;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                sheet.Column(7).Width = Math.Min(80, sheet.Column(7).Width);
                workbook.SaveAs(filePath);
            }
        }

        public void ExportNomenclatureAnalyticsReport(DateTime from, DateTime to, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));
            if (_nomenclature == null)
                throw new InvalidOperationException("ReportService не настроен для отчётов СЭД (нет INomenclatureRepository).");

            // Аналогично ExportDocumentVolumeReport: расширяем `to` до конца
            // дня, чтобы захватить документы, зарегистрированные после полуночи
            // на последний день периода.
            var toInclusive = ExtendToEndOfDay(to);
            var docs = _documents.Search(new DocumentSearchFilter { From = from, To = toInclusive });
            var cases = _nomenclature.ListCases(year: null, activeOnly: false);
            var depts = _nomenclature.ListDepartments();

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Номенклатура");
                sheet.Cell(1, 1).Value = "Аналитика по номенклатуре дел";
                sheet.Range(1, 1, 1, 6).Merge().Style.Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";
                sheet.Range(2, 1, 2, 6).Merge();

                int hdr = 4;
                sheet.Cell(hdr, 1).Value = "Индекс";
                sheet.Cell(hdr, 2).Value = "Дело";
                sheet.Cell(hdr, 3).Value = "Отдел";
                sheet.Cell(hdr, 4).Value = "Срок хранения, лет";
                sheet.Cell(hdr, 5).Value = "Документов за период";
                sheet.Cell(hdr, 6).Value = "Активно";
                var headerRange = sheet.Range(hdr, 1, hdr, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = hdr + 1;
                foreach (var c in cases.OrderBy(x => x.Index))
                {
                    int count = docs.Count(d => d.NomenclatureCaseId == c.Id);
                    var dept = depts.FirstOrDefault(x => x.Id == c.DepartmentId);
                    sheet.Cell(row, 1).Value = c.Index;
                    sheet.Cell(row, 2).Value = c.Title;
                    sheet.Cell(row, 3).Value = dept?.Name ?? "—";
                    sheet.Cell(row, 4).Value = c.RetentionPeriodYears;
                    sheet.Cell(row, 5).Value = count;
                    sheet.Cell(row, 6).Value = c.IsActive ? "Да" : "Нет";
                    row++;
                }

                sheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        private static string FormatDirection(DocumentDirection d)
        {
            switch (d)
            {
                case DocumentDirection.Incoming: return "Входящий";
                case DocumentDirection.Outgoing: return "Исходящий";
                case DocumentDirection.Internal: return "Внутренний";
                case DocumentDirection.Directive: return "Распорядительный";
                default: return d.ToString();
            }
        }

        /// <summary>
        /// Возвращает дату-время «конец суток» для значения, пришедшего из
        /// DatePicker (где время = 00:00). Используется при формировании
        /// отчётов с периодом, чтобы полуинтервал работал ожидаемо «по дни
        /// включительно». Если на вход уже передан момент времени с ненулевым
        /// временем — возвращается без изменений.
        /// </summary>
        private static DateTime ExtendToEndOfDay(DateTime to)
        {
            if (to.TimeOfDay == TimeSpan.Zero)
                return to.Date.AddDays(1).AddTicks(-1);
            return to;
        }

        private static string FormatDocumentType(DocumentType t)
        {
            switch (t)
            {
                case DocumentType.Internal: return "Внутренний";
                case DocumentType.Archive: return "Архивный";
                case DocumentType.It: return "ИТ";
                default: return t.ToString();
            }
        }

        private static string FormatDocumentStatus(DocumentStatus s)
        {
            switch (s)
            {
                case DocumentStatus.New: return "Новый";
                case DocumentStatus.InProgress: return "В работе";
                case DocumentStatus.OnHold: return "Отложен";
                case DocumentStatus.Completed: return "Завершён";
                case DocumentStatus.Cancelled: return "Отменён";
                case DocumentStatus.Registered: return "Зарегистрирован";
                default: return s.ToString();
            }
        }

        private static string SafeSheetName(string name)
        {
            // Excel-ограничения: ≤31 символ, без []:*?/\.
            var trimmed = (name ?? "Лист").Trim();
            foreach (var ch in new[] { '[', ']', ':', '*', '?', '/', '\\' })
                trimmed = trimmed.Replace(ch, ' ');
            if (trimmed.Length > 31) trimmed = trimmed.Substring(0, 31);
            return string.IsNullOrEmpty(trimmed) ? "Лист" : trimmed;
        }

        private static string FormatArchiveRequestKind(ArchiveRequestKind kind)
        {
            switch (kind)
            {
                case ArchiveRequestKind.SocialLegal:
                    return "социально-правовой запрос";
                case ArchiveRequestKind.Thematic:
                    return "тематический запрос";
                case ArchiveRequestKind.MunicipalLegalActCopy:
                    return "копия муниципального правового акта";
                case ArchiveRequestKind.PaidThematic:
                    return "платный тематический запрос";
                default:
                    return kind.ToString();
            }
        }

        private static string FormatVehicleStatus(VehicleStatus status)
        {
            switch (status)
            {
                case VehicleStatus.Available: return "Доступен";
                case VehicleStatus.OnMission: return "В рейсе";
                case VehicleStatus.Maintenance: return "На обслуживании";
                default: return status.ToString();
            }
        }

        private static string FormatActionType(AuditActionType action)
        {
            switch (action)
            {
                case AuditActionType.Created: return "Создание";
                case AuditActionType.Updated: return "Изменение";
                case AuditActionType.Deleted: return "Удаление";
                case AuditActionType.StatusChanged: return "Смена статуса";
                case AuditActionType.Registered: return "Регистрация";
                case AuditActionType.AssignedToCase: return "Прикреплено к делу";
                case AuditActionType.AttachmentAdded: return "Добавлено вложение";
                case AuditActionType.AttachmentVersioned: return "Новая версия";
                case AuditActionType.AttachmentRemoved: return "Удалено вложение";
                case AuditActionType.AttachmentViewed: return "Просмотр вложения";
                case AuditActionType.ResolutionIssued: return "Резолюция";
                case AuditActionType.TaskAssigned: return "Поручение";
                case AuditActionType.TaskCompleted: return "Поручение исп.";
                case AuditActionType.TaskOverdue: return "Поручение просрочено";
                case AuditActionType.TaskReassigned: return "Поручение переназн.";
                case AuditActionType.ApprovalSent: return "Маршрут запущен";
                case AuditActionType.ApprovalSigned: return "Согласовано";
                case AuditActionType.ApprovalRejected: return "Отклонено";
                case AuditActionType.InventoryTransactionRecorded: return "Движение ТМЦ";
                case AuditActionType.VehicleTripBooked: return "Путевой лист";
                case AuditActionType.ArchiveRequestProcessed: return "Архивный запрос";
                case AuditActionType.ItTicketResolved: return "ИТ-заявка";
                case AuditActionType.SignatureAdded: return "Подписание";
                case AuditActionType.SignatureRevoked: return "Отзыв подписи";
                case AuditActionType.DocumentLocked: return "Блокировка КЭП";
                case AuditActionType.DocumentUnlocked: return "Разблокировка";
                case AuditActionType.NotificationSent: return "Уведомление";
                case AuditActionType.SubstitutionCreated: return "Замещение создано";
                case AuditActionType.SubstitutionCancelled: return "Замещение отменено";
                case AuditActionType.TaskDelegated: return "Делегирование";
                case AuditActionType.IndexRebuilt: return "Индексация";
                case AuditActionType.ReportGenerated: return "Отчёт";
                case AuditActionType.UserLogin: return "Вход";
                case AuditActionType.UserLogout: return "Выход";
                default: return action.ToString();
            }
        }

        private static string FormatCategory(InventoryCategory category)
        {
            switch (category)
            {
                case InventoryCategory.Stationery:
                    return "Канцелярские товары и бланки";
                case InventoryCategory.IT_Equipment:
                    return "Оргтехника, расходные материалы и связь";
                case InventoryCategory.Cleaning_Supplies:
                    return "Хозяйственные и эксплуатационные материалы";
                default:
                    return category.ToString();
            }
        }

        private static W.Paragraph Paragraph(string text)
        {
            var p = new W.Paragraph();
            var run = p.AppendChild(new W.Run());
            run.AppendChild(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return p;
        }

        private static W.Paragraph Heading(string text)
        {
            var p = new W.Paragraph();
            var props = p.AppendChild(new W.ParagraphProperties());
            props.AppendChild(new W.Justification { Val = W.JustificationValues.Center });
            var run = p.AppendChild(new W.Run());
            var runProps = run.AppendChild(new W.RunProperties());
            runProps.AppendChild(new W.Bold());
            runProps.AppendChild(new W.FontSize { Val = "32" });
            run.AppendChild(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return p;
        }

        // ================================================================
        // Phase 12 — пакет регламентированных отчётов СЭД
        // ================================================================

        public void ExportOutgoingDispatchRegistry(DateTime from, DateTime to, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));
            if (to < from) throw new ArgumentException("Период некорректен (To < From).");

            var rows = _documents.Search(new DocumentSearchFilter
            {
                Direction = DocumentDirection.Outgoing,
                From = from,
                To = to.Date.AddDays(1).AddTicks(-1),
                RegisteredOnly = true,
            });

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Реестр отправки");
                sheet.Cell(1, 1).Value = $"Реестр отправки исходящих за {from:dd.MM.yyyy}–{to:dd.MM.yyyy}";
                var title = sheet.Range(1, 1, 1, 6);
                title.Merge();
                title.Style.Font.Bold = true;
                title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                sheet.Cell(2, 1).Value = "№";
                sheet.Cell(2, 2).Value = "Рег. номер";
                sheet.Cell(2, 3).Value = "Дата";
                sheet.Cell(2, 4).Value = "Тема";
                sheet.Cell(2, 5).Value = "Корреспондент";
                sheet.Cell(2, 6).Value = "Способ отправки";
                var header = sheet.Range(2, 1, 2, 6);
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;

                int r = 3;
                int n = 1;
                foreach (var d in rows)
                {
                    sheet.Cell(r, 1).Value = n++;
                    sheet.Cell(r, 2).Value = d.RegistrationNumber;
                    sheet.Cell(r, 3).Value = d.RegistrationDate ?? d.CreationDate;
                    sheet.Cell(r, 3).Style.DateFormat.Format = "dd.MM.yyyy";
                    sheet.Cell(r, 4).Value = d.Title;
                    sheet.Cell(r, 5).Value = d.Correspondent;
                    sheet.Cell(r, 6).Value = "Почта/Эл.почта";
                    r++;
                }

                sheet.Cell(r, 1).Value = $"Всего отправлено: {n - 1}";
                sheet.Range(r, 1, r, 6).Merge().Style.Font.Italic = true;
                sheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        public void GenerateCaseInventory(int nomenclatureCaseId, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));
            if (_nomenclature == null)
                throw new InvalidOperationException("INomenclatureRepository не зарегистрирован.");

            var @case = _nomenclature.GetCase(nomenclatureCaseId)
                ?? throw new InvalidOperationException($"Дело #{nomenclatureCaseId} не найдено.");

            var documents = _documents.Search(new DocumentSearchFilter
            {
                NomenclatureCaseId = nomenclatureCaseId,
            });

            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new W.Document(new W.Body());
                var body = main.Document.Body;

                body.AppendChild(Heading($"ОПИСЬ ДЕЛА № {@case.Index}"));
                body.AppendChild(Paragraph($"«{@case.Title}»"));
                body.AppendChild(Paragraph($"Год: {@case.Year}; срок хранения: {@case.RetentionPeriodYears} лет."));
                body.AppendChild(Paragraph(string.Empty));

                int idx = 1;
                foreach (var d in documents.OrderBy(x => x.RegistrationDate ?? x.CreationDate))
                {
                    body.AppendChild(Paragraph(
                        $"{idx}. {d.RegistrationNumber} от {(d.RegistrationDate ?? d.CreationDate):dd.MM.yyyy} — {d.Title}"));
                    idx++;
                }

                body.AppendChild(Paragraph(string.Empty));
                body.AppendChild(Paragraph($"Всего в дело включено документов: {documents.Count}."));
                body.AppendChild(Paragraph($"Опись составлена: {DateTime.Now:dd.MM.yyyy}."));
                main.Document.Save();
            }
        }

        public void ExportFleetReport(DateTime from, DateTime to, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));
            if (to < from) throw new ArgumentException("Период некорректен (To < From).");
            if (_vehicles == null)
                throw new InvalidOperationException("IVehicleRepository не зарегистрирован.");

            var until = to.Date.AddDays(1).AddTicks(-1);

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Парк");
                sheet.Cell(1, 1).Value = $"Отчёт по парку за {from:dd.MM.yyyy}–{to:dd.MM.yyyy}";
                sheet.Range(1, 1, 1, 7).Merge().Style.Font.Bold = true;

                sheet.Cell(2, 1).Value = "ТС";
                sheet.Cell(2, 2).Value = "Гос. номер";
                sheet.Cell(2, 3).Value = "Поездок";
                sheet.Cell(2, 4).Value = "Часов в работе";
                sheet.Cell(2, 5).Value = "Часов простоя";
                sheet.Cell(2, 6).Value = "Заявок (документов)";
                sheet.Cell(2, 7).Value = "Статус";
                sheet.Range(2, 1, 2, 7).Style.Font.Bold = true;
                sheet.Range(2, 1, 2, 7).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;

                int r = 3;
                double periodHours = (until - from).TotalHours;
                foreach (var v in _vehicles.ListVehicles())
                {
                    var trips = _vehicles.ListTrips(v.Id)
                        .Where(t => t.EndDate >= from && t.StartDate <= until)
                        .ToList();

                    double busy = trips.Sum(t =>
                    {
                        var s = t.StartDate < from ? from : t.StartDate;
                        var e = t.EndDate > until ? until : t.EndDate;
                        return (e - s).TotalHours;
                    });

                    sheet.Cell(r, 1).Value = v.Model;
                    sheet.Cell(r, 2).Value = v.LicensePlate;
                    sheet.Cell(r, 3).Value = trips.Count;
                    sheet.Cell(r, 4).Value = Math.Round(busy, 1);
                    sheet.Cell(r, 5).Value = Math.Round(Math.Max(0, periodHours - busy), 1);
                    sheet.Cell(r, 6).Value = trips.Count(t => t.DocumentId.HasValue);
                    sheet.Cell(r, 7).Value = FormatVehicleStatus(v.CurrentStatus);
                    r++;
                }
                sheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        public void ExportInventoryTurnoverReport(DateTime from, DateTime to, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));
            if (to < from) throw new ArgumentException("Период некорректен (To < From).");

            var until = to.Date.AddDays(1).AddTicks(-1);

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Оборот склада");
                sheet.Cell(1, 1).Value = $"Оборот склада за {from:dd.MM.yyyy}–{to:dd.MM.yyyy}";
                sheet.Range(1, 1, 1, 6).Merge().Style.Font.Bold = true;

                sheet.Cell(2, 1).Value = "Наименование";
                sheet.Cell(2, 2).Value = "Категория";
                sheet.Cell(2, 3).Value = "Остаток на начало";
                sheet.Cell(2, 4).Value = "Приход";
                sheet.Cell(2, 5).Value = "Расход";
                sheet.Cell(2, 6).Value = "Остаток на конец";
                sheet.Range(2, 1, 2, 6).Style.Font.Bold = true;
                sheet.Range(2, 1, 2, 6).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;

                int r = 3;
                foreach (var item in _inventory.ListItems().OrderBy(i => i.Name))
                {
                    var transactions = _inventory.ListTransactions(item.Id);
                    int beforeFrom = transactions.Where(t => t.TransactionDate < from)
                        .Sum(t => t.QuantityChanged);
                    int incoming = transactions
                        .Where(t => t.TransactionDate >= from && t.TransactionDate <= until && t.QuantityChanged > 0)
                        .Sum(t => t.QuantityChanged);
                    int outgoing = -transactions
                        .Where(t => t.TransactionDate >= from && t.TransactionDate <= until && t.QuantityChanged < 0)
                        .Sum(t => t.QuantityChanged);
                    int closing = beforeFrom + incoming - outgoing;

                    sheet.Cell(r, 1).Value = item.Name;
                    sheet.Cell(r, 2).Value = FormatCategory(item.Category);
                    sheet.Cell(r, 3).Value = beforeFrom;
                    sheet.Cell(r, 4).Value = incoming;
                    sheet.Cell(r, 5).Value = outgoing;
                    sheet.Cell(r, 6).Value = closing;
                    r++;
                }
                sheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        public void ExportDocumentAuditTrail(int documentId, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));
            if (_audit == null)
                throw new InvalidOperationException("IAuditService не зарегистрирован.");

            var doc = _documents.GetById(documentId)
                ?? throw new InvalidOperationException($"Документ #{documentId} не найден.");

            var entries = _audit.Query(new AuditQueryFilter
            {
                EntityType = nameof(Document),
                EntityId = documentId,
            });

            var pdf = new MinimalPdfWriter();
            pdf.AddLine($"История документа {doc.RegistrationNumber}");
            pdf.AddLine(new string('=', 60));
            pdf.AddLine($"Заголовок: {doc.Title}");
            pdf.AddLine($"Создан: {doc.CreationDate:dd.MM.yyyy HH:mm}");
            pdf.AddLine($"Всего записей в журнале аудита: {entries.Count}");
            pdf.AddBlank();
            pdf.AddLine(string.Format("{0,-19} {1,-18} {2,-6} {3}", "Время", "Действие", "Актор", "Hash"));
            pdf.AddLine(new string('-', 80));

            foreach (var e in entries.OrderBy(x => x.Timestamp))
            {
                var hashShort = string.IsNullOrEmpty(e.Hash) ? "" : e.Hash.Substring(0, Math.Min(12, e.Hash.Length));
                pdf.AddLine(string.Format("{0,-19} {1,-18} {2,-6} {3}",
                    e.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"),
                    Truncate(FormatActionType(e.ActionType), 22),
                    e.UserId?.ToString() ?? "—",
                    hashShort));
                var details = string.IsNullOrEmpty(e.Details) ? e.NewValues : e.Details;
                if (!string.IsNullOrEmpty(details))
                    pdf.AddLine("    " + Truncate(details, 90));
            }

            pdf.AddBlank();
            pdf.AddLine($"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            pdf.Save(filePath);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        // ------------------------------------------------------------
        // Phase 15 / Improvement #12 — журналы регистрации.
        // ------------------------------------------------------------

        public void ExportFuelLog(IEnumerable<VehicleTrip> trips, DateTime from, DateTime to, string filePath)
        {
            if (trips == null) throw new ArgumentNullException(nameof(trips));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            var rows = trips
                .Where(t => t != null)
                .OrderBy(t => t.ActualStart ?? t.StartDate)
                .ToList();

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Журнал ГСМ");
                sheet.Cell(1, 1).Value = "Журнал учёта ГСМ";
                sheet.Range(1, 1, 1, 11).Merge().Style
                    .Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";
                sheet.Range(2, 1, 2, 11).Merge();

                int header = 4;
                sheet.Cell(header, 1).Value = "Дата";
                sheet.Cell(header, 2).Value = "ТС (марка, гос. №)";
                sheet.Cell(header, 3).Value = "Топливо";
                sheet.Cell(header, 4).Value = "Водитель";
                sheet.Cell(header, 5).Value = "Маршрут";
                sheet.Cell(header, 6).Value = "Одометр старт";
                sheet.Cell(header, 7).Value = "Одометр финиш";
                sheet.Cell(header, 8).Value = "Пробег, км";
                sheet.Cell(header, 9).Value = "Выдано, л";
                sheet.Cell(header, 10).Value = "Расход, л";
                sheet.Cell(header, 11).Value = "Документ-основание";

                var hdr = sheet.Range(header, 1, header, 11);
                hdr.Style.Font.Bold = true;
                hdr.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hdr.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = header + 1;
                foreach (var t in rows)
                {
                    var vehicle = t.Vehicle;
                    sheet.Cell(row, 1).Value = (t.ActualStart ?? t.StartDate).ToString("dd.MM.yyyy");
                    sheet.Cell(row, 2).Value = vehicle != null
                        ? $"{vehicle.Model} {vehicle.LicensePlate}".Trim()
                        : "—";
                    sheet.Cell(row, 3).Value = vehicle != null ? FormatFuelType(vehicle.FuelType) : "—";
                    sheet.Cell(row, 4).Value = string.IsNullOrWhiteSpace(t.DriverName) ? "—" : t.DriverName;
                    sheet.Cell(row, 5).Value = t.Route ?? string.Empty;
                    sheet.Cell(row, 6).Value = t.OdometerStart?.ToString() ?? "—";
                    sheet.Cell(row, 7).Value = t.OdometerEnd?.ToString() ?? "—";
                    sheet.Cell(row, 8).Value = t.DistanceKm?.ToString() ?? "—";
                    sheet.Cell(row, 9).Value = t.FuelIssuedLiters?.ToString("0.##") ?? "—";
                    sheet.Cell(row, 10).Value = t.FuelUsedLiters?.ToString("0.##") ?? "—";
                    var basis = t.BasisDocument ?? t.Document;
                    sheet.Cell(row, 11).Value = basis != null
                        ? (basis.RegistrationNumber ?? basis.Title ?? string.Empty)
                        : string.Empty;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                sheet.Column(5).Width = Math.Min(60, sheet.Column(5).Width);
                workbook.SaveAs(filePath);
            }
        }

        public void ExportSafetyBriefingsJournal(IEnumerable<SafetyBriefing> briefings, string filePath)
        {
            if (briefings == null) throw new ArgumentNullException(nameof(briefings));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            var rows = briefings.Where(b => b != null).OrderBy(b => b.BriefingDate).ToList();

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Журнал ОТ_ПБ");
                sheet.Cell(1, 1).Value = "Журнал инструктажей по охране труда / пожарной безопасности";
                sheet.Range(1, 1, 1, 7).Merge().Style
                    .Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}";
                sheet.Range(2, 1, 2, 7).Merge();

                int header = 4;
                sheet.Cell(header, 1).Value = "Дата";
                sheet.Cell(header, 2).Value = "Вид инструктажа";
                sheet.Cell(header, 3).Value = "Тема";
                sheet.Cell(header, 4).Value = "Инструктируемый";
                sheet.Cell(header, 5).Value = "Инструктор";
                sheet.Cell(header, 6).Value = "Подпись";
                sheet.Cell(header, 7).Value = "Примечания";

                var hdr = sheet.Range(header, 1, header, 7);
                hdr.Style.Font.Bold = true;
                hdr.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hdr.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = header + 1;
                foreach (var b in rows)
                {
                    sheet.Cell(row, 1).Value = b.BriefingDate.ToString("dd.MM.yyyy");
                    sheet.Cell(row, 2).Value = FormatBriefingKind(b.Kind);
                    sheet.Cell(row, 3).Value = b.Topic ?? string.Empty;
                    sheet.Cell(row, 4).Value = b.TraineeEmployee?.FullName ?? "—";
                    sheet.Cell(row, 5).Value = b.InstructorEmployee?.FullName ?? "—";
                    sheet.Cell(row, 6).Value = b.SignatureConfirmed ? "Подписано" : "Не подписано";
                    sheet.Cell(row, 7).Value = b.Notes ?? string.Empty;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                sheet.Column(3).Width = Math.Min(50, sheet.Column(3).Width);
                sheet.Column(7).Width = Math.Min(50, sheet.Column(7).Width);
                workbook.SaveAs(filePath);
            }
        }

        public void ExportInventarizationsJournal(IEnumerable<Inventarization> inventarizations, string filePath)
        {
            if (inventarizations == null) throw new ArgumentNullException(nameof(inventarizations));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            var rows = inventarizations.Where(i => i != null).OrderBy(i => i.StartDate).ToList();

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Журнал инвентаризаций");
                sheet.Cell(1, 1).Value = "Журнал инвентаризаций";
                sheet.Range(1, 1, 1, 8).Merge().Style
                    .Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}";
                sheet.Range(2, 1, 2, 8).Merge();

                int header = 4;
                sheet.Cell(header, 1).Value = "Дата начала";
                sheet.Cell(header, 2).Value = "Дата окончания";
                sheet.Cell(header, 3).Value = "Объект";
                sheet.Cell(header, 4).Value = "Описание объекта";
                sheet.Cell(header, 5).Value = "Председатель";
                sheet.Cell(header, 6).Value = "Состав комиссии";
                sheet.Cell(header, 7).Value = "Расхождений";
                sheet.Cell(header, 8).Value = "Документ-акт";

                var hdr = sheet.Range(header, 1, header, 8);
                hdr.Style.Font.Bold = true;
                hdr.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hdr.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = header + 1;
                foreach (var inv in rows)
                {
                    sheet.Cell(row, 1).Value = inv.StartDate.ToString("dd.MM.yyyy");
                    sheet.Cell(row, 2).Value = inv.EndDate?.ToString("dd.MM.yyyy") ?? "—";
                    sheet.Cell(row, 3).Value = FormatInventarizationScope(inv.Scope);
                    sheet.Cell(row, 4).Value = inv.ScopeDescription ?? string.Empty;
                    sheet.Cell(row, 5).Value = inv.Chairman?.FullName ?? "—";
                    sheet.Cell(row, 6).Value = inv.CommissionMembers ?? string.Empty;
                    sheet.Cell(row, 7).Value = inv.Discrepancies?.Count ?? 0;
                    sheet.Cell(row, 8).Value = inv.ResultDocument?.RegistrationNumber
                        ?? inv.ResultDocument?.Title
                        ?? string.Empty;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                sheet.Column(4).Width = Math.Min(50, sheet.Column(4).Width);
                sheet.Column(6).Width = Math.Min(50, sheet.Column(6).Width);
                workbook.SaveAs(filePath);
            }
        }

        public void ExportArchiveTransferJournal(IEnumerable<ArchiveTransfer> transfers, string filePath)
        {
            if (transfers == null) throw new ArgumentNullException(nameof(transfers));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            var rows = transfers.Where(t => t != null).OrderBy(t => t.TransferDate).ToList();

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Журнал передачи в архив");
                sheet.Cell(1, 1).Value = "Журнал передачи дел в архив";
                sheet.Range(1, 1, 1, 9).Merge().Style
                    .Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}";
                sheet.Range(2, 1, 2, 9).Merge();

                int header = 4;
                sheet.Cell(header, 1).Value = "Дата";
                sheet.Cell(header, 2).Value = "Дело (индекс)";
                sheet.Cell(header, 3).Value = "Заголовок дела";
                sheet.Cell(header, 4).Value = "Архивный шифр";
                sheet.Cell(header, 5).Value = "Передал";
                sheet.Cell(header, 6).Value = "Принял";
                sheet.Cell(header, 7).Value = "Акт";
                sheet.Cell(header, 8).Value = "Срок хранения";
                sheet.Cell(header, 9).Value = "Заметки";

                var hdr = sheet.Range(header, 1, header, 9);
                hdr.Style.Font.Bold = true;
                hdr.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hdr.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = header + 1;
                foreach (var t in rows)
                {
                    sheet.Cell(row, 1).Value = t.TransferDate.ToString("dd.MM.yyyy");
                    sheet.Cell(row, 2).Value = t.NomenclatureCase?.Index ?? "—";
                    sheet.Cell(row, 3).Value = t.NomenclatureCase?.Title ?? string.Empty;
                    sheet.Cell(row, 4).Value = t.ArchiveCode ?? string.Empty;
                    sheet.Cell(row, 5).Value = t.TransferredBy?.FullName ?? "—";
                    sheet.Cell(row, 6).Value = t.AcceptedBy?.FullName ?? "—";
                    sheet.Cell(row, 7).Value = t.ActDocument?.RegistrationNumber
                        ?? t.ActDocument?.Title
                        ?? string.Empty;
                    sheet.Cell(row, 8).Value = t.RetentionYears > 0
                        ? $"{t.RetentionYears} лет"
                        : "Постоянно";
                    sheet.Cell(row, 9).Value = t.Notes ?? string.Empty;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                sheet.Column(3).Width = Math.Min(50, sheet.Column(3).Width);
                sheet.Column(9).Width = Math.Min(40, sheet.Column(9).Width);
                workbook.SaveAs(filePath);
            }
        }

        public void ExportContractsJournal(IEnumerable<Document> contracts, DateTime from, DateTime to, string filePath)
        {
            if (contracts == null) throw new ArgumentNullException(nameof(contracts));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            ExportRegistrationJournal(contracts, $"Журнал договоров за {from:dd.MM.yyyy} — {to:dd.MM.yyyy}", filePath);
        }

        private static string FormatFuelType(FuelType fuel)
        {
            switch (fuel)
            {
                case FuelType.Petrol: return "Бензин";
                case FuelType.Diesel: return "Дизель";
                case FuelType.Gas: return "Газ";
                case FuelType.Electric: return "Электро";
                case FuelType.Hybrid: return "Гибрид";
                default: return fuel.ToString();
            }
        }

        private static string FormatBriefingKind(BriefingKind kind)
        {
            switch (kind)
            {
                case BriefingKind.Initial: return "Вводный";
                case BriefingKind.PrimaryAtWorkplace: return "Первичный на рабочем месте";
                case BriefingKind.Recurring: return "Повторный";
                case BriefingKind.Targeted: return "Целевой";
                case BriefingKind.Unscheduled: return "Внеплановый";
                default: return kind.ToString();
            }
        }

        private static string FormatInventarizationScope(InventarizationScope scope)
        {
            switch (scope)
            {
                case InventarizationScope.Inventory: return "Склад ТМЦ";
                case InventarizationScope.FixedAssets: return "Основные средства";
                case InventarizationScope.Documents: return "Номенклатура дел";
                case InventarizationScope.Premises: return "Помещения";
                case InventarizationScope.Vehicles: return "Транспорт";
                case InventarizationScope.Other: return "Иное";
                default: return scope.ToString();
            }
        }
    }
}
