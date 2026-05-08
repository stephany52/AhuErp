using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Bug #7. Фасеточный фильтр единого центра РКК. Описывает срез документов
    /// независимо от того, как пользователь к нему пришёл — через дерево фасетов
    /// слева, через выбор сохранённого пресета (<see cref="RkkPreset"/>) или
    /// через расширенный поиск.
    ///
    /// Поля проектируются так, чтобы:
    /// 1) UI мог биндиться напрямую к facet-перечислениям;
    /// 2) <see cref="ToSearchFilter(int?)"/> детерминированно переводил их в
    ///    общий <see cref="DocumentSearchFilter"/> для <see cref="IDocumentRepository.Search"/>.
    ///
    /// Часть критериев (<see cref="DocumentRoleFacet.Approver"/>,
    /// <see cref="DocumentRoleFacet.Signer"/>, <see cref="DocumentDeadlineFacet"/>)
    /// требует объединения с другими таблицами либо клиентских вычислений на
    /// основе текущей даты — для них предусмотрены пост-фильтры в коллекции
    /// результатов через <see cref="ApplyClientSidePostFilters"/>.
    /// </summary>
    public sealed class DocumentFilter
    {
        public DocumentTypeFacet Type { get; set; } = DocumentTypeFacet.All;
        public DocumentStatusFacet Status { get; set; } = DocumentStatusFacet.All;
        public DocumentRoleFacet MyRole { get; set; } = DocumentRoleFacet.All;
        public DocumentDeadlineFacet Deadline { get; set; } = DocumentDeadlineFacet.All;
        public int? NomenclatureCaseId { get; set; }

        /// <summary>Полнотекстовый запрос (Title/Summary/RegistrationNumber/Correspondent/IncomingNumber).</summary>
        public string SearchText { get; set; }

        /// <summary>Произвольная нижняя граница периода (опциональная, расширенный поиск).</summary>
        public DateTime? PeriodFrom { get; set; }

        /// <summary>Произвольная верхняя граница периода (опциональная, расширенный поиск).</summary>
        public DateTime? PeriodTo { get; set; }

        /// <summary>
        /// Только документы с непустым <see cref="Document.RegistrationNumber"/>
        /// (зарегистрированные). Прокидывается в
        /// <see cref="DocumentSearchFilter.RegisteredOnly"/>. Используется
        /// пресетом «Журналы регистрации»: журнал должен показывать всё,
        /// что было зарегистрировано, независимо от текущего статуса.
        /// </summary>
        public bool RegisteredOnly { get; set; }

        /// <summary>
        /// Перевод фасеточного фильтра в общий <see cref="DocumentSearchFilter"/>
        /// для серверной/инмемори-выборки. <paramref name="currentEmployeeId"/>
        /// нужен для фасета «Моя роль = Я исполнитель / Я автор».
        /// </summary>
        public DocumentSearchFilter ToSearchFilter(int? currentEmployeeId)
        {
            var f = new DocumentSearchFilter
            {
                Text = SearchText,
                NomenclatureCaseId = NomenclatureCaseId,
                From = PeriodFrom,
                To = PeriodTo,
                RegisteredOnly = RegisteredOnly,
            };

            switch (Type)
            {
                case DocumentTypeFacet.Incoming:
                    f.Direction = DocumentDirection.Incoming;
                    break;
                case DocumentTypeFacet.Outgoing:
                    f.Direction = DocumentDirection.Outgoing;
                    break;
                case DocumentTypeFacet.Internal:
                    f.Direction = DocumentDirection.Internal;
                    break;
                case DocumentTypeFacet.All:
                case DocumentTypeFacet.OfficeDocuments:
                case DocumentTypeFacet.Contracts:
                case DocumentTypeFacet.ServiceMemos:
                case DocumentTypeFacet.ItTickets:
                case DocumentTypeFacet.ArchiveRequests:
                case DocumentTypeFacet.VehicleTrips:
                case DocumentTypeFacet.WriteOffs:
                    // Эти подвиды требуют клиентского постфильтра по
                    // конкретному наследнику Document или подкатегории —
                    // остаются в ApplyClientSidePostFilters().
                    break;
            }

            switch (Status)
            {
                case DocumentStatusFacet.Draft:
                    f.Status = DocumentStatus.New;
                    break;
                case DocumentStatusFacet.Registered:
                    f.Status = DocumentStatus.Registered;
                    break;
                case DocumentStatusFacet.OnApproval:
                    // Phase 13: точный статус. До этого фасет «На согласовании»
                    // отображался через InProgress, теперь — через выделенный
                    // DocumentStatus.OnApproval.
                    f.Status = DocumentStatus.OnApproval;
                    break;
                case DocumentStatusFacet.Approved:
                    f.Status = DocumentStatus.Approved;
                    break;
                case DocumentStatusFacet.Rejected:
                    f.Status = DocumentStatus.Rejected;
                    break;
                case DocumentStatusFacet.OnSigning:
                    f.Status = DocumentStatus.OnSigning;
                    break;
                case DocumentStatusFacet.Signed:
                    f.Status = DocumentStatus.Signed;
                    break;
                case DocumentStatusFacet.OnExecution:
                    // Включаем legacy InProgress как эквивалент OnExecution —
                    // данные, созданные до Phase 13, остаются видимыми.
                    f.StatusIn = new[]
                    {
                        DocumentStatus.OnExecution,
                        DocumentStatus.InProgress,
                    };
                    break;
                case DocumentStatusFacet.Completed:
                    f.Status = DocumentStatus.Completed;
                    break;
                case DocumentStatusFacet.Cancelled:
                    f.Status = DocumentStatus.Cancelled;
                    break;
                case DocumentStatusFacet.Archived:
                    f.Status = DocumentStatus.Archived;
                    break;
                case DocumentStatusFacet.Overdue:
                    f.OverdueOnly = true;
                    break;
                case DocumentStatusFacet.NotCompleted:
                    f.StatusIn = new[]
                    {
                        DocumentStatus.New,
                        DocumentStatus.InProgress,
                        DocumentStatus.OnHold,
                        DocumentStatus.Registered,
                        DocumentStatus.OnApproval,
                        DocumentStatus.Approved,
                        DocumentStatus.Rejected,
                        DocumentStatus.OnSigning,
                        DocumentStatus.Signed,
                        DocumentStatus.OnExecution,
                    };
                    break;
                case DocumentStatusFacet.All:
                    break;
            }

            switch (MyRole)
            {
                case DocumentRoleFacet.Executor:
                    if (currentEmployeeId.HasValue)
                        f.AssignedEmployeeId = currentEmployeeId.Value;
                    break;
                case DocumentRoleFacet.Author:
                case DocumentRoleFacet.Approver:
                case DocumentRoleFacet.Signer:
                    // Author / Approver / Signer обрабатываются клиентским
                    // постфильтром (Author — по полю AuthorId, остальные — через
                    // вспомогательные репозитории, которые знает только VM).
                    break;
                case DocumentRoleFacet.All:
                    break;
            }

            if (Deadline == DocumentDeadlineFacet.Overdue)
            {
                f.OverdueOnly = true;
            }
            // ThisWeek / NextWeek / NoDeadline считаются на клиенте.

            return f;
        }

        /// <summary>
        /// Клиентские постфильтры, которые нельзя выразить через
        /// <see cref="DocumentSearchFilter"/>. Вызывается уже над результатом
        /// <see cref="IDocumentRepository.Search"/>.
        /// </summary>
        public IReadOnlyList<Document> ApplyClientSidePostFilters(
            IEnumerable<Document> source,
            int? currentEmployeeId,
            DateTime now)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            IEnumerable<Document> q = source;

            switch (Type)
            {
                case DocumentTypeFacet.OfficeDocuments:
                    // Старый раздел «Документационное обеспечение» показывал
                    // и входящие, и внутренние документы (см. OfficeViewModel.
                    // Reload). DocumentTypeFacet — single-value enum, поэтому
                    // объединение направлений выражается на клиенте.
                    q = q.Where(d => d.Direction == DocumentDirection.Incoming
                                     || d.Direction == DocumentDirection.Internal);
                    break;
                case DocumentTypeFacet.Contracts:
                    q = q.Where(d => d.DocumentTypeRef != null
                                     && d.DocumentTypeRef.ShortCode != null
                                     && d.DocumentTypeRef.ShortCode.IndexOf("contract", StringComparison.OrdinalIgnoreCase) >= 0);
                    break;
                case DocumentTypeFacet.ServiceMemos:
                    q = q.Where(d => d.DocumentTypeRef != null
                                     && d.DocumentTypeRef.ShortCode != null
                                     && (d.DocumentTypeRef.ShortCode.IndexOf("memo", StringComparison.OrdinalIgnoreCase) >= 0
                                         || d.DocumentTypeRef.ShortCode.IndexOf("служ", StringComparison.OrdinalIgnoreCase) >= 0));
                    break;
                case DocumentTypeFacet.ItTickets:
                    q = q.Where(d => d is ItTicket || d.Type == DocumentType.It);
                    break;
                case DocumentTypeFacet.ArchiveRequests:
                    q = q.Where(d => d is ArchiveRequest
                                     || d.Type == DocumentType.Archive
                                     || d.Type == DocumentType.ArchiveRequest);
                    break;
                case DocumentTypeFacet.VehicleTrips:
                    q = q.Where(d => d.VehicleTrips != null && d.VehicleTrips.Count > 0);
                    break;
                case DocumentTypeFacet.WriteOffs:
                    // Старое поведение брало все Internal-документы, что
                    // включало служебные записки, распоряжения и пр. Акты
                    // списания ТМЦ имеют ShortCode = «АКТ» (см. EfDataSeeder)
                    // и Name «Акт списания ТМЦ» — фильтруем по ShortCode/Name,
                    // как это сделано для Contracts/ServiceMemos.
                    q = q.Where(d => d.DocumentTypeRef != null
                                     && ((d.DocumentTypeRef.ShortCode != null
                                          && (d.DocumentTypeRef.ShortCode.IndexOf("АКТ", StringComparison.OrdinalIgnoreCase) >= 0
                                              || d.DocumentTypeRef.ShortCode.IndexOf("писан", StringComparison.OrdinalIgnoreCase) >= 0
                                              || d.DocumentTypeRef.ShortCode.IndexOf("writeoff", StringComparison.OrdinalIgnoreCase) >= 0))
                                         || (d.DocumentTypeRef.Name != null
                                             && d.DocumentTypeRef.Name.IndexOf("писан", StringComparison.OrdinalIgnoreCase) >= 0)));
                    break;
            }

            if (MyRole == DocumentRoleFacet.Author && currentEmployeeId.HasValue)
            {
                var meId = currentEmployeeId.Value;
                q = q.Where(d => d.AuthorId == meId);
            }

            switch (Deadline)
            {
                case DocumentDeadlineFacet.ThisWeek:
                    {
                        var endOfWeek = EndOfWeek(now);
                        q = q.Where(d => d.Deadline != default
                                         && d.Deadline >= now.Date
                                         && d.Deadline <= endOfWeek);
                        break;
                    }
                case DocumentDeadlineFacet.NextWeek:
                    {
                        // Старт следующей недели — понедельник 00:00:00.
                        // EndOfWeek возвращает воскресенье 23:59:59, поэтому
                        // .Date.AddDays(1) даёт ровно начало понедельника.
                        var startNext = EndOfWeek(now).Date.AddDays(1);
                        var endNext = startNext.AddDays(6).Date.AddDays(1).AddSeconds(-1);
                        q = q.Where(d => d.Deadline != default
                                         && d.Deadline >= startNext
                                         && d.Deadline <= endNext);
                        break;
                    }
                case DocumentDeadlineFacet.NoDeadline:
                    q = q.Where(d => d.Deadline == default);
                    break;
            }

            return q.ToList().AsReadOnly();
        }

        /// <summary>
        /// Конец «этой недели» (воскресенье 23:59:59). Используем
        /// российский календарь: неделя начинается с понедельника.
        /// </summary>
        public static DateTime EndOfWeek(DateTime now)
        {
            int delta = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
            return now.Date.AddDays(delta).AddDays(1).AddSeconds(-1);
        }
    }

    /// <summary>Тип документа в фасеточной фильтрации РКК.</summary>
    public enum DocumentTypeFacet
    {
        All,
        Incoming,
        Outgoing,
        Internal,

        /// <summary>
        /// Композитное значение «документационное обеспечение» —
        /// объединение Incoming + Internal (старый раздел OfficeView).
        /// </summary>
        OfficeDocuments,

        Contracts,
        ServiceMemos,
        ItTickets,
        ArchiveRequests,
        VehicleTrips,
        WriteOffs,
    }

    /// <summary>Статус документа в фасеточной фильтрации РКК.</summary>
    public enum DocumentStatusFacet
    {
        All,
        Draft,
        Registered,
        OnApproval,
        Approved,
        Rejected,
        OnSigning,
        Signed,
        OnExecution,
        Completed,
        Cancelled,
        Archived,
        Overdue,
        NotCompleted,
    }

    /// <summary>Моя роль в документе.</summary>
    public enum DocumentRoleFacet
    {
        All,
        Author,
        Executor,
        Approver,
        Signer,
    }

    /// <summary>Срез документов по дедлайну.</summary>
    public enum DocumentDeadlineFacet
    {
        All,
        Overdue,
        ThisWeek,
        NextWeek,
        NoDeadline,
    }

    /// <summary>
    /// Сохранённые пресеты единого центра РКК. Заменяют отдельные
    /// разделы «Документационное обеспечение», «Мои задачи», «Архивный
    /// отдел», «ИТО», «Журналы регистрации», «Поиск».
    /// </summary>
    public enum RkkPreset
    {
        /// <summary>Все документы (РКК по умолчанию).</summary>
        All,

        /// <summary>«Документационное обеспечение» — входящие/внутренние.</summary>
        OfficeDocuments,

        /// <summary>«Мои задачи» — я исполнитель и статус ≠ «Завершён».</summary>
        MyTasks,

        /// <summary>«Архивный отдел» — архивные запросы.</summary>
        Archive,

        /// <summary>«ИТО» — IT-заявки.</summary>
        ItService,

        /// <summary>«Журналы регистрации» — только зарегистрированные.</summary>
        Journals,

        /// <summary>«Поиск» — режим расширенного поиска (попап + полнотекстовая строка).</summary>
        Search,
    }

    /// <summary>
    /// Хелперы получения <see cref="DocumentFilter"/> по выбранному пресету.
    /// </summary>
    public static class RkkPresets
    {
        public static DocumentFilter Build(RkkPreset preset)
        {
            switch (preset)
            {
                case RkkPreset.OfficeDocuments:
                    // Документационное обеспечение = входящие + внутренние
                    // (см. OfficeViewModel.Reload). Объединение направлений
                    // выражено через DocumentTypeFacet.OfficeDocuments.
                    return new DocumentFilter { Type = DocumentTypeFacet.OfficeDocuments };
                case RkkPreset.MyTasks:
                    return new DocumentFilter
                    {
                        MyRole = DocumentRoleFacet.Executor,
                        Status = DocumentStatusFacet.NotCompleted,
                    };
                case RkkPreset.Archive:
                    return new DocumentFilter { Type = DocumentTypeFacet.ArchiveRequests };
                case RkkPreset.ItService:
                    return new DocumentFilter { Type = DocumentTypeFacet.ItTickets };
                case RkkPreset.Journals:
                    // Журналы регистрации — все документы с непустым
                    // RegistrationNumber, независимо от текущего статуса
                    // (паритет с JournalViewModel.BuildFilter).
                    return new DocumentFilter { RegisteredOnly = true };
                case RkkPreset.Search:
                    return new DocumentFilter();
                case RkkPreset.All:
                default:
                    return new DocumentFilter();
            }
        }
    }
}
