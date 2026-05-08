using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Bug #7. Unit-тесты на <see cref="DocumentFilter"/> и пресеты
    /// единого центра РКК (<see cref="RkkPreset"/>):
    /// перевод фасет-фильтра в <see cref="DocumentSearchFilter"/>,
    /// клиентские пост-фильтры (Author / дедлайн), пресеты
    /// «Документационное обеспечение» / «Мои задачи» / «Архивный отдел» /
    /// «ИТО» / «Журналы регистрации» / «Поиск».
    /// </summary>
    public class DocumentFilterTests
    {
        // ---------------- ToSearchFilter — перевод фасетов в SearchFilter

        [Fact]
        public void ToSearchFilter_Type_Incoming_sets_direction_incoming()
        {
            var f = new DocumentFilter { Type = DocumentTypeFacet.Incoming };
            var s = f.ToSearchFilter(currentEmployeeId: 1);
            Assert.Equal(DocumentDirection.Incoming, s.Direction);
        }

        [Fact]
        public void ToSearchFilter_Type_Outgoing_sets_direction_outgoing()
        {
            var f = new DocumentFilter { Type = DocumentTypeFacet.Outgoing };
            var s = f.ToSearchFilter(currentEmployeeId: 1);
            Assert.Equal(DocumentDirection.Outgoing, s.Direction);
        }

        [Fact]
        public void ToSearchFilter_Type_Internal_sets_direction_internal()
        {
            var f = new DocumentFilter { Type = DocumentTypeFacet.Internal };
            var s = f.ToSearchFilter(currentEmployeeId: 1);
            Assert.Equal(DocumentDirection.Internal, s.Direction);
        }

        [Fact]
        public void ToSearchFilter_Type_All_does_not_set_direction()
        {
            var f = new DocumentFilter { Type = DocumentTypeFacet.All };
            var s = f.ToSearchFilter(currentEmployeeId: null);
            Assert.Null(s.Direction);
        }

        [Theory]
        [InlineData(DocumentTypeFacet.Contracts)]
        [InlineData(DocumentTypeFacet.ServiceMemos)]
        [InlineData(DocumentTypeFacet.ItTickets)]
        [InlineData(DocumentTypeFacet.ArchiveRequests)]
        [InlineData(DocumentTypeFacet.VehicleTrips)]
        [InlineData(DocumentTypeFacet.WriteOffs)]
        [InlineData(DocumentTypeFacet.OfficeDocuments)]
        public void ToSearchFilter_subtype_facets_dont_set_direction(DocumentTypeFacet facet)
        {
            var f = new DocumentFilter { Type = facet };
            var s = f.ToSearchFilter(null);
            // Эти фасеты не могут быть выражены через DocumentDirection —
            // они применяются клиентским постфильтром.
            Assert.Null(s.Direction);
        }

        [Theory]
        [InlineData(DocumentStatusFacet.Draft, DocumentStatus.New)]
        [InlineData(DocumentStatusFacet.Registered, DocumentStatus.Registered)]
        [InlineData(DocumentStatusFacet.OnApproval, DocumentStatus.OnApproval)]
        [InlineData(DocumentStatusFacet.Approved, DocumentStatus.Approved)]
        [InlineData(DocumentStatusFacet.Rejected, DocumentStatus.Rejected)]
        [InlineData(DocumentStatusFacet.OnSigning, DocumentStatus.OnSigning)]
        [InlineData(DocumentStatusFacet.Signed, DocumentStatus.Signed)]
        [InlineData(DocumentStatusFacet.Completed, DocumentStatus.Completed)]
        [InlineData(DocumentStatusFacet.Cancelled, DocumentStatus.Cancelled)]
        [InlineData(DocumentStatusFacet.Archived, DocumentStatus.Archived)]
        public void ToSearchFilter_Status_maps_to_DocumentStatus(DocumentStatusFacet facet, DocumentStatus expected)
        {
            // Phase 13: фасетный статус соответствует одному из 11 значений
            // DocumentStatus один-к-одному (legacy-маппинг через InProgress
            // заменён на точные подстатусы делопроизводства).
            var f = new DocumentFilter { Status = facet };
            var s = f.ToSearchFilter(null);
            Assert.Equal(expected, s.Status);
        }

        [Fact]
        public void ToSearchFilter_OnExecution_facet_includes_legacy_InProgress()
        {
            // Phase 13: фасет «На исполнении» агрегирует и точный статус
            // OnExecution (новые документы), и legacy InProgress (документы
            // до Phase 13).
            var f = new DocumentFilter { Status = DocumentStatusFacet.OnExecution };
            var s = f.ToSearchFilter(null);
            Assert.Null(s.Status);
            Assert.NotNull(s.StatusIn);
            Assert.Contains(DocumentStatus.OnExecution, s.StatusIn);
            Assert.Contains(DocumentStatus.InProgress, s.StatusIn);
        }

        [Fact]
        public void ToSearchFilter_Status_Overdue_sets_OverdueOnly()
        {
            var f = new DocumentFilter { Status = DocumentStatusFacet.Overdue };
            var s = f.ToSearchFilter(null);
            Assert.True(s.OverdueOnly);
            Assert.Null(s.Status);
        }

        [Fact]
        public void ToSearchFilter_Status_NotCompleted_sets_StatusIn_active_subset()
        {
            var f = new DocumentFilter { Status = DocumentStatusFacet.NotCompleted };
            var s = f.ToSearchFilter(null);
            Assert.NotNull(s.StatusIn);
            Assert.Contains(DocumentStatus.New, s.StatusIn);
            Assert.Contains(DocumentStatus.InProgress, s.StatusIn);
            Assert.Contains(DocumentStatus.Registered, s.StatusIn);
            Assert.DoesNotContain(DocumentStatus.Completed, s.StatusIn);
            Assert.DoesNotContain(DocumentStatus.Cancelled, s.StatusIn);
        }

        [Fact]
        public void ToSearchFilter_MyRole_Executor_sets_AssignedEmployeeId()
        {
            var f = new DocumentFilter { MyRole = DocumentRoleFacet.Executor };
            var s = f.ToSearchFilter(currentEmployeeId: 42);
            Assert.Equal(42, s.AssignedEmployeeId);
        }

        [Fact]
        public void ToSearchFilter_MyRole_Executor_without_employee_id_does_not_set_assigned()
        {
            var f = new DocumentFilter { MyRole = DocumentRoleFacet.Executor };
            var s = f.ToSearchFilter(currentEmployeeId: null);
            Assert.Null(s.AssignedEmployeeId);
        }

        [Fact]
        public void ToSearchFilter_MyRole_Author_does_not_set_AssignedEmployeeId()
        {
            // Author — клиентский постфильтр по AuthorId, а не AssignedEmployeeId.
            var f = new DocumentFilter { MyRole = DocumentRoleFacet.Author };
            var s = f.ToSearchFilter(currentEmployeeId: 42);
            Assert.Null(s.AssignedEmployeeId);
        }

        [Fact]
        public void ToSearchFilter_Deadline_Overdue_sets_OverdueOnly()
        {
            var f = new DocumentFilter { Deadline = DocumentDeadlineFacet.Overdue };
            var s = f.ToSearchFilter(null);
            Assert.True(s.OverdueOnly);
        }

        [Fact]
        public void ToSearchFilter_passes_through_search_text_period_and_case_id()
        {
            var f = new DocumentFilter
            {
                SearchText = "ИФНС",
                NomenclatureCaseId = 7,
                PeriodFrom = new DateTime(2025, 1, 1),
                PeriodTo = new DateTime(2025, 12, 31),
            };
            var s = f.ToSearchFilter(null);
            Assert.Equal("ИФНС", s.Text);
            Assert.Equal(7, s.NomenclatureCaseId);
            Assert.Equal(new DateTime(2025, 1, 1), s.From);
            Assert.Equal(new DateTime(2025, 12, 31), s.To);
        }

        // ---------------- ApplyClientSidePostFilters — клиентские пост-фильтры

        [Fact]
        public void PostFilter_ItTickets_keeps_only_ItTicket_or_It_type()
        {
            var ticket = new ItTicket { Id = 1, Title = "PC repair" };
            var officeDoc = new Document { Id = 2, Title = "Letter", Type = DocumentType.Office };
            var itDoc = new Document { Id = 3, Title = "IT-задача", Type = DocumentType.It };

            var f = new DocumentFilter { Type = DocumentTypeFacet.ItTickets };
            var result = f.ApplyClientSidePostFilters(
                new Document[] { ticket, officeDoc, itDoc },
                currentEmployeeId: null,
                now: DateTime.Now);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, d => d.Id == ticket.Id);
            Assert.Contains(result, d => d.Id == itDoc.Id);
            Assert.DoesNotContain(result, d => d.Id == officeDoc.Id);
        }

        [Fact]
        public void PostFilter_ArchiveRequests_keeps_only_archive_subclass_or_archive_type()
        {
            var req = new ArchiveRequest { Id = 1, Title = "Архивная справка" };
            var office = new Document { Id = 2, Title = "Office", Type = DocumentType.Office };
            var explicitArchive = new Document { Id = 3, Title = "Архив", Type = DocumentType.Archive };

            var f = new DocumentFilter { Type = DocumentTypeFacet.ArchiveRequests };
            var result = f.ApplyClientSidePostFilters(
                new Document[] { req, office, explicitArchive },
                null, DateTime.Now);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, d => d.Id == req.Id);
            Assert.Contains(result, d => d.Id == explicitArchive.Id);
        }

        [Fact]
        public void PostFilter_Author_keeps_only_documents_of_current_employee()
        {
            var mine = new Document { Id = 1, AuthorId = 100 };
            var others = new Document { Id = 2, AuthorId = 200 };

            var f = new DocumentFilter { MyRole = DocumentRoleFacet.Author };
            var result = f.ApplyClientSidePostFilters(
                new[] { mine, others },
                currentEmployeeId: 100,
                now: DateTime.Now);

            Assert.Single(result);
            Assert.Equal(mine.Id, result[0].Id);
        }

        [Fact]
        public void PostFilter_NoDeadline_keeps_only_documents_without_deadline()
        {
            var withDeadline = new Document { Id = 1, Deadline = new DateTime(2099, 1, 1) };
            var noDeadline = new Document { Id = 2 }; // Deadline == default

            var f = new DocumentFilter { Deadline = DocumentDeadlineFacet.NoDeadline };
            var result = f.ApplyClientSidePostFilters(
                new[] { withDeadline, noDeadline },
                null, DateTime.Now);

            Assert.Single(result);
            Assert.Equal(noDeadline.Id, result[0].Id);
        }

        [Fact]
        public void PostFilter_ThisWeek_keeps_only_deadlines_within_current_week()
        {
            // Берём понедельник 2025-09-01 как «сейчас» — конец недели = воскресенье 2025-09-07 23:59:59.
            var now = new DateTime(2025, 9, 1, 10, 0, 0);
            var inWeek = new Document { Id = 1, Deadline = new DateTime(2025, 9, 5) };
            var nextWeek = new Document { Id = 2, Deadline = new DateTime(2025, 9, 10) };
            var lastWeek = new Document { Id = 3, Deadline = new DateTime(2025, 8, 31) };

            var f = new DocumentFilter { Deadline = DocumentDeadlineFacet.ThisWeek };
            var result = f.ApplyClientSidePostFilters(
                new[] { inWeek, nextWeek, lastWeek }, null, now);

            Assert.Single(result);
            Assert.Equal(inWeek.Id, result[0].Id);
        }

        [Fact]
        public void PostFilter_NextWeek_keeps_only_deadlines_in_next_calendar_week()
        {
            var now = new DateTime(2025, 9, 1, 10, 0, 0); // понедельник
            var inWeek = new Document { Id = 1, Deadline = new DateTime(2025, 9, 5) };
            var nextWeek = new Document { Id = 2, Deadline = new DateTime(2025, 9, 10) };
            var lateNextWeek = new Document { Id = 3, Deadline = new DateTime(2025, 9, 14) }; // воскресенье следующей недели
            var afterNextWeek = new Document { Id = 4, Deadline = new DateTime(2025, 9, 15) };

            var f = new DocumentFilter { Deadline = DocumentDeadlineFacet.NextWeek };
            var result = f.ApplyClientSidePostFilters(
                new[] { inWeek, nextWeek, lateNextWeek, afterNextWeek }, null, now);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, d => d.Id == nextWeek.Id);
            Assert.Contains(result, d => d.Id == lateNextWeek.Id);
        }

        [Fact]
        public void PostFilter_NextWeek_includes_monday_morning_of_next_week()
        {
            // Регрессионный тест: ранее startNext = EndOfWeek(now).AddDays(1)
            // давал понедельник 23:59:59, и документ с дедлайном «понедельник
            // 09:00 следующей недели» не попадал в выборку. После фикса
            // startNext должен быть понедельник 00:00:00.
            var now = new DateTime(2025, 9, 1, 10, 0, 0); // понедельник
            var mondayMorningNextWeek = new Document
            {
                Id = 1,
                Deadline = new DateTime(2025, 9, 8, 9, 0, 0),
            };

            var f = new DocumentFilter { Deadline = DocumentDeadlineFacet.NextWeek };
            var result = f.ApplyClientSidePostFilters(
                new[] { mondayMorningNextWeek }, null, now);

            Assert.Single(result);
            Assert.Equal(mondayMorningNextWeek.Id, result[0].Id);
        }

        [Fact]
        public void EndOfWeek_returns_sunday_2359_for_any_weekday()
        {
            // 2025-09-03 (среда) → конец недели = 2025-09-07 (воскресенье) 23:59:59.
            var endOfWeek = DocumentFilter.EndOfWeek(new DateTime(2025, 9, 3, 12, 0, 0));
            Assert.Equal(new DateTime(2025, 9, 7, 23, 59, 59), endOfWeek);
        }

        [Fact]
        public void ApplyClientSidePostFilters_throws_on_null_source()
        {
            var f = new DocumentFilter();
            Assert.Throws<ArgumentNullException>(() =>
                f.ApplyClientSidePostFilters(null, null, DateTime.Now));
        }

        // ---------------- RkkPresets.Build — сохранённые пресеты

        [Fact]
        public void Preset_All_returns_default_filter()
        {
            var f = RkkPresets.Build(RkkPreset.All);
            Assert.Equal(DocumentTypeFacet.All, f.Type);
            Assert.Equal(DocumentStatusFacet.All, f.Status);
            Assert.Equal(DocumentRoleFacet.All, f.MyRole);
            Assert.Equal(DocumentDeadlineFacet.All, f.Deadline);
            Assert.Null(f.NomenclatureCaseId);
        }

        [Fact]
        public void Preset_OfficeDocuments_includes_incoming_and_internal()
        {
            // Старый OfficeViewModel загружал ListByType(Incoming)
            // .Concat(ListByType(Internal)). Пресет «Документационное
            // обеспечение» должен сохранять это объединение.
            var f = RkkPresets.Build(RkkPreset.OfficeDocuments);
            Assert.Equal(DocumentTypeFacet.OfficeDocuments, f.Type);
        }

        [Fact]
        public void PostFilter_OfficeDocuments_keeps_incoming_and_internal_drops_outgoing()
        {
            // Регрессия: раньше пресет OfficeDocuments был
            // = DocumentTypeFacet.Incoming, и внутренние документы
            // выпадали из раздела. Теперь OfficeDocuments — это
            // отдельный фасет с клиентским постфильтром.
            var incoming = new Document
            {
                Id = 1,
                Title = "ИФНС",
                Direction = DocumentDirection.Incoming,
                Type = DocumentType.Incoming,
            };
            var internalDoc = new Document
            {
                Id = 2,
                Title = "Служебная записка",
                Direction = DocumentDirection.Internal,
                Type = DocumentType.Internal,
            };
            var outgoing = new Document
            {
                Id = 3,
                Title = "Ответ внешнему адресату",
                Direction = DocumentDirection.Outgoing,
                Type = DocumentType.Office,
            };

            var f = new DocumentFilter { Type = DocumentTypeFacet.OfficeDocuments };
            var result = f.ApplyClientSidePostFilters(
                new[] { incoming, internalDoc, outgoing },
                currentEmployeeId: null,
                now: DateTime.Now);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, d => d.Id == incoming.Id);
            Assert.Contains(result, d => d.Id == internalDoc.Id);
            Assert.DoesNotContain(result, d => d.Id == outgoing.Id);
        }

        [Fact]
        public void PostFilter_WriteOffs_keeps_only_writeoff_acts_drops_other_internals()
        {
            // Регрессия: раньше WriteOffs матчил любой DocumentType.Internal,
            // что включало служебные записки и распоряжения. Теперь фильтр
            // привязан к ShortCode/Name «АКТ» / «списан» (паритет с
            // DocumentTypeRef «Акт списания ТМЦ» из EfDataSeeder).
            var actType = new DocumentTypeRef
            {
                Id = 1,
                Name = "Акт списания ТМЦ",
                ShortCode = "АКТ",
                DefaultDirection = DocumentDirection.Internal,
                IsActive = true,
            };
            var memoType = new DocumentTypeRef
            {
                Id = 2,
                Name = "Служебная записка",
                ShortCode = "СЛУЖ",
                DefaultDirection = DocumentDirection.Internal,
                IsActive = true,
            };

            var writeOff = new Document
            {
                Id = 1,
                Title = "Акт списания ноутбука",
                Type = DocumentType.Internal,
                Direction = DocumentDirection.Internal,
                DocumentTypeRef = actType,
            };
            var memo = new Document
            {
                Id = 2,
                Title = "Служебная записка",
                Type = DocumentType.Internal,
                Direction = DocumentDirection.Internal,
                DocumentTypeRef = memoType,
            };
            var memoWithoutRef = new Document
            {
                Id = 3,
                Title = "Распоряжение",
                Type = DocumentType.Internal,
                Direction = DocumentDirection.Internal,
            };

            var f = new DocumentFilter { Type = DocumentTypeFacet.WriteOffs };
            var result = f.ApplyClientSidePostFilters(
                new[] { writeOff, memo, memoWithoutRef },
                currentEmployeeId: null,
                now: DateTime.Now);

            Assert.Single(result);
            Assert.Equal(writeOff.Id, result[0].Id);
        }

        [Fact]
        public void Preset_MyTasks_is_executor_plus_not_completed()
        {
            var f = RkkPresets.Build(RkkPreset.MyTasks);
            Assert.Equal(DocumentRoleFacet.Executor, f.MyRole);
            Assert.Equal(DocumentStatusFacet.NotCompleted, f.Status);
        }

        [Fact]
        public void Preset_Archive_filters_archive_requests()
        {
            var f = RkkPresets.Build(RkkPreset.Archive);
            Assert.Equal(DocumentTypeFacet.ArchiveRequests, f.Type);
        }

        [Fact]
        public void Preset_ItService_filters_it_tickets()
        {
            var f = RkkPresets.Build(RkkPreset.ItService);
            Assert.Equal(DocumentTypeFacet.ItTickets, f.Type);
        }

        [Fact]
        public void Preset_Journals_filters_registered_only()
        {
            // Старый JournalViewModel использовал RegisteredOnly = true
            // — журнал показывает все документы с непустым
            // RegistrationNumber, независимо от их текущего статуса.
            // Раньше пресет выставлял Status = Registered, что срезало
            // документы, уже переведённые в InProgress / Completed.
            var f = RkkPresets.Build(RkkPreset.Journals);
            Assert.True(f.RegisteredOnly);
            Assert.Equal(DocumentStatusFacet.All, f.Status);
        }

        [Fact]
        public void ToSearchFilter_passes_through_RegisteredOnly()
        {
            var f = new DocumentFilter { RegisteredOnly = true };
            var s = f.ToSearchFilter(currentEmployeeId: null);
            Assert.True(s.RegisteredOnly);
        }

        [Fact]
        public void Filter_Journals_via_repo_includes_registered_documents_in_any_status()
        {
            // Регрессия: «Журналы регистрации» раньше выставлял
            // Status = Registered, и документ с рег. номером и статусом
            // InProgress не попадал в журнал. Проверяем, что после
            // перехода на RegisteredOnly журнал собирает все
            // зарегистрированные документы.
            var repo = new InMemoryDocumentRepository();
            repo.Add(new Document
            {
                Title = "Договор поставки",
                Direction = DocumentDirection.Outgoing,
                Type = DocumentType.Office,
                Status = DocumentStatus.InProgress,
                CreationDate = new DateTime(2025, 1, 5),
                RegistrationNumber = "ИСХ-1/2025",
                RegistrationDate = new DateTime(2025, 1, 6),
            });
            repo.Add(new Document
            {
                Title = "Письмо ИФНС",
                Direction = DocumentDirection.Incoming,
                Type = DocumentType.Incoming,
                Status = DocumentStatus.Completed,
                CreationDate = new DateTime(2025, 1, 10),
                RegistrationNumber = "ВХ-7/2025",
                RegistrationDate = new DateTime(2025, 1, 11),
            });
            repo.Add(new Document
            {
                Title = "Черновик",
                Direction = DocumentDirection.Internal,
                Type = DocumentType.Internal,
                Status = DocumentStatus.New,
                CreationDate = new DateTime(2025, 1, 12),
                // Без рег. номера — в журналы попасть не должен.
            });

            var f = RkkPresets.Build(RkkPreset.Journals);
            var result = repo.Search(f.ToSearchFilter(null));

            Assert.Equal(2, result.Count);
            Assert.Contains(result, d => d.RegistrationNumber == "ИСХ-1/2025");
            Assert.Contains(result, d => d.RegistrationNumber == "ВХ-7/2025");
        }

        [Fact]
        public void Preset_Search_returns_empty_filter()
        {
            var f = RkkPresets.Build(RkkPreset.Search);
            Assert.Equal(DocumentTypeFacet.All, f.Type);
            Assert.Equal(DocumentStatusFacet.All, f.Status);
            Assert.Equal(DocumentRoleFacet.All, f.MyRole);
        }

        // ---------------- Интеграция фильтра с InMemoryDocumentRepository --

        [Fact]
        public void Filter_Incoming_with_text_filters_documents_via_repo_search()
        {
            var repo = new InMemoryDocumentRepository();
            repo.Add(new Document
            {
                Title = "Письмо ИФНС",
                Direction = DocumentDirection.Incoming,
                Type = DocumentType.Incoming,
                Status = DocumentStatus.New,
                CreationDate = new DateTime(2025, 3, 1),
                RegistrationNumber = "ВХ-1/2025",
                RegistrationDate = new DateTime(2025, 3, 2),
                Correspondent = "ИФНС № 26",
            });
            repo.Add(new Document
            {
                Title = "Распоряжение",
                Direction = DocumentDirection.Internal,
                Type = DocumentType.Internal,
                Status = DocumentStatus.New,
                CreationDate = new DateTime(2025, 3, 3),
            });

            var f = new DocumentFilter
            {
                Type = DocumentTypeFacet.Incoming,
                SearchText = "ИФНС",
            };
            var search = f.ToSearchFilter(currentEmployeeId: null);
            var result = repo.Search(search);

            Assert.Single(result);
            Assert.Equal("ВХ-1/2025", result[0].RegistrationNumber);
        }

        [Fact]
        public void Filter_Executor_role_filters_assigned_employee_via_repo()
        {
            var repo = new InMemoryDocumentRepository();
            repo.Add(new Document
            {
                Title = "Моё",
                Direction = DocumentDirection.Internal,
                Type = DocumentType.Internal,
                CreationDate = DateTime.Today,
                AssignedEmployeeId = 5,
            });
            repo.Add(new Document
            {
                Title = "Чужое",
                Direction = DocumentDirection.Internal,
                Type = DocumentType.Internal,
                CreationDate = DateTime.Today,
                AssignedEmployeeId = 9,
            });

            var f = new DocumentFilter { MyRole = DocumentRoleFacet.Executor };
            var search = f.ToSearchFilter(currentEmployeeId: 5);
            var result = repo.Search(search);

            Assert.Single(result);
            Assert.Equal("Моё", result[0].Title);
        }
    }
}
