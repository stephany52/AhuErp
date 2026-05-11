using System;
using System.IO;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 19 / Improvement #16 — архив и долговременное хранение
    /// (акты о выделении к уничтожению + ответы архива).
    /// Покрытие:
    /// (1) <see cref="InMemoryDestructionActRepository"/> — Add / Get /
    ///     GetByActNumber / List / ListByStatus / уникальность ActNumber,
    ///     каскадная подгрузка <see cref="DestructionActItem"/>.
    /// (2) <see cref="ArchiveRetentionService"/> — FindEligibleForDestruction
    ///     (по сроку хранения), стейт-машина Draft → Approved → Executed |
    ///     Cancelled, денормализованный снимок дел, аудит транзитов.
    /// (3) <see cref="ReportService"/> — DOCX-выгрузка акта (не падает,
    ///     создаёт непустой файл) и DOCX-ответ архива (справка/выписка/копия).
    /// </summary>
    public class Phase19ArchiveRetentionTests
    {
        private readonly InMemoryDestructionActRepository _acts = new InMemoryDestructionActRepository();
        private readonly InMemoryNomenclatureRepository _nomenclature = new InMemoryNomenclatureRepository();
        private readonly InMemoryDocumentRepository _documents = new InMemoryDocumentRepository();
        private readonly InMemoryAuditLogRepository _audit = new InMemoryAuditLogRepository();
        private readonly InMemoryInventoryRepository _inventory = new InMemoryInventoryRepository();
        private readonly ArchiveRetentionService _service;
        private readonly ReportService _reports;

        public Phase19ArchiveRetentionTests()
        {
            _service = new ArchiveRetentionService(_nomenclature, _documents, _acts, _audit);
            _reports = new ReportService(
                _inventory, _documents,
                tasks: null, taskRepo: null, nomenclature: _nomenclature,
                vehicles: null, audit: null,
                destructionActs: _acts);
        }

        // =========================================================
        // (1) InMemoryDestructionActRepository.
        // =========================================================

        [Fact]
        public void Repository_Add_assigns_id_and_persists_items()
        {
            var act = new DestructionAct
            {
                ActNumber = "АКТ-2026-001",
                ActDate = new DateTime(2026, 5, 1),
                DraftedByEmployeeId = 1,
            };
            act.Items.Add(new DestructionActItem
            {
                CaseIndex = "01-07", CaseTitle = "Переписка по АХЧ", CaseYear = 2020, RetentionYears = 5
            });

            var saved = _acts.Add(act);
            Assert.True(saved.Id > 0);
            Assert.True(saved.Items.First().Id > 0);

            var loaded = _acts.Get(saved.Id);
            Assert.NotNull(loaded);
            Assert.Single(loaded.Items);
            Assert.Equal("01-07", loaded.Items.First().CaseIndex);
        }

        [Fact]
        public void Repository_Add_rejects_duplicate_act_number()
        {
            _acts.Add(new DestructionAct { ActNumber = "АКТ-1", ActDate = DateTime.Today, DraftedByEmployeeId = 1 });
            Assert.Throws<InvalidOperationException>(() =>
                _acts.Add(new DestructionAct { ActNumber = "АКТ-1", ActDate = DateTime.Today, DraftedByEmployeeId = 2 }));
        }

        [Fact]
        public void Repository_GetByActNumber_returns_match_or_null()
        {
            _acts.Add(new DestructionAct { ActNumber = "АКТ-X", ActDate = DateTime.Today, DraftedByEmployeeId = 1 });
            Assert.NotNull(_acts.GetByActNumber("АКТ-X"));
            Assert.Null(_acts.GetByActNumber("АКТ-Y"));
        }

        [Fact]
        public void Repository_ListByStatus_filters_and_orders_by_date_desc()
        {
            var a1 = _acts.Add(new DestructionAct { ActNumber = "А1", ActDate = new DateTime(2024, 1, 1), DraftedByEmployeeId = 1 });
            var a2 = _acts.Add(new DestructionAct { ActNumber = "А2", ActDate = new DateTime(2026, 1, 1), DraftedByEmployeeId = 1 });
            var a3 = _acts.Add(new DestructionAct
            {
                ActNumber = "А3", ActDate = new DateTime(2025, 1, 1), DraftedByEmployeeId = 1,
                Status = DestructionStatus.Executed,
            });

            var draft = _acts.ListByStatus(DestructionStatus.Draft);
            Assert.Equal(new[] { a2.Id, a1.Id }, draft.Select(a => a.Id).ToArray());

            var executed = _acts.ListByStatus(DestructionStatus.Executed);
            Assert.Single(executed);
            Assert.Equal(a3.Id, executed[0].Id);
        }

        [Fact]
        public void Repository_RemoveItem_removes_persistence_but_keeps_act()
        {
            var act = _acts.Add(new DestructionAct { ActNumber = "А1", ActDate = DateTime.Today, DraftedByEmployeeId = 1 });
            var item = _acts.AddItem(new DestructionActItem
            {
                DestructionActId = act.Id, CaseIndex = "01-07", CaseTitle = "T", CaseYear = 2020, RetentionYears = 5
            });

            _acts.RemoveItem(item.Id);
            var loaded = _acts.Get(act.Id);
            Assert.NotNull(loaded);
            Assert.Empty(loaded.Items);
        }

        // =========================================================
        // (2) ArchiveRetentionService.
        // =========================================================

        [Fact]
        public void FindEligibleForDestruction_returns_cases_with_expired_retention()
        {
            // 5-летний срок, год 2020 → истёк к 2026: 2020+5=2025 ≤ 2026.
            var expired = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-07", Title = "Старое дело", Year = 2020, RetentionPeriodYears = 5
            });
            // 75-летний срок, год 2020 → НЕ истёк к 2026.
            _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-08", Title = "Кадровое", Year = 2020, RetentionPeriodYears = 75
            });
            // Постоянное хранение.
            _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-01", Title = "Уставы", Year = 2010, RetentionPeriodYears = 0
            });

            var found = _service.FindEligibleForDestruction(new DateTime(2026, 5, 1));
            Assert.Single(found);
            Assert.Equal(expired.Id, found[0].Id);

            var last = _audit.GetLast();
            Assert.NotNull(last);
            Assert.Equal(AuditActionType.RetentionScanCompleted, last.ActionType);
        }

        [Fact]
        public void FindEligibleForDestruction_includes_inactive_cases()
        {
            var inactive = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-09", Title = "Закрытое дело", Year = 2018,
                RetentionPeriodYears = 5, IsActive = false
            });

            var found = _service.FindEligibleForDestruction(new DateTime(2026, 5, 1));
            Assert.Contains(found, c => c.Id == inactive.Id);
        }

        [Fact]
        public void DraftAct_captures_denormalized_snapshot_and_audit()
        {
            var nc = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "02-15", Title = "Переписка с ЖКХ", Year = 2019,
                RetentionPeriodYears = 5, Article = "ст. 88"
            });
            // Положим в дело два документа — DocumentCount должен быть 2.
            _documents.Add(new Document { Title = "Письмо 1", NomenclatureCaseId = nc.Id, RegistrationNumber = "ВХ-1", CreationDate = DateTime.Today });
            _documents.Add(new Document { Title = "Письмо 2", NomenclatureCaseId = nc.Id, RegistrationNumber = "ВХ-2", CreationDate = DateTime.Today });

            var act = _service.DraftAct("АКТ-2026-007", new DateTime(2026, 5, 1),
                draftedByEmployeeId: 11, caseIds: new[] { nc.Id }, notes: "Протокол ЭПК №3.");

            Assert.Equal(DestructionStatus.Draft, act.Status);
            Assert.Single(act.Items);
            var item = act.Items.First();
            Assert.Equal("02-15", item.CaseIndex);
            Assert.Equal("Переписка с ЖКХ", item.CaseTitle);
            Assert.Equal(2019, item.CaseYear);
            Assert.Equal(5, item.RetentionYears);
            Assert.Equal("ст. 88", item.Article);
            Assert.Equal(2, item.DocumentCount);

            // Снимок не должен ссылаться на исходный объект NomenclatureCase.
            nc.Title = "Изменённый заголовок";
            _nomenclature.UpdateCase(nc);
            Assert.Equal("Переписка с ЖКХ", _acts.Get(act.Id).Items.First().CaseTitle);

            var audit = _audit.GetLast();
            Assert.Equal(AuditActionType.DestructionActDrafted, audit.ActionType);
            Assert.Equal(11, audit.UserId);
        }

        [Fact]
        public void DraftAct_rejects_permanent_storage_case()
        {
            var permanent = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-01", Title = "Уставы", Year = 2010, RetentionPeriodYears = 0
            });

            Assert.Throws<InvalidOperationException>(() =>
                _service.DraftAct("А-1", DateTime.Today, draftedByEmployeeId: 1,
                    caseIds: new[] { permanent.Id }));
        }

        [Fact]
        public void DraftAct_rejects_empty_case_list()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.DraftAct("А-1", DateTime.Today, draftedByEmployeeId: 1,
                    caseIds: Array.Empty<int>()));
        }

        [Fact]
        public void DraftAct_rejects_unknown_case()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _service.DraftAct("А-1", DateTime.Today, draftedByEmployeeId: 1,
                    caseIds: new[] { 9999 }));
        }

        [Fact]
        public void DraftAct_deduplicates_case_ids()
        {
            var nc = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-07", Title = "T", Year = 2020, RetentionPeriodYears = 5
            });
            var act = _service.DraftAct("А-1", DateTime.Today, 1, new[] { nc.Id, nc.Id, nc.Id });
            Assert.Single(act.Items);
        }

        [Fact]
        public void ApproveAct_transitions_draft_to_approved_with_audit()
        {
            var nc = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-07", Title = "T", Year = 2020, RetentionPeriodYears = 5
            });
            var act = _service.DraftAct("А-1", DateTime.Today, 1, new[] { nc.Id });

            var approved = _service.ApproveAct(act.Id, approvedByEmployeeId: 5,
                approvedAt: new DateTime(2026, 5, 2));

            Assert.Equal(DestructionStatus.Approved, approved.Status);
            Assert.Equal(5, approved.ApprovedByEmployeeId);
            Assert.Equal(new DateTime(2026, 5, 2), approved.ApprovedAt);

            var audit = _audit.GetLast();
            Assert.Equal(AuditActionType.DestructionActApproved, audit.ActionType);
        }

        [Fact]
        public void ApproveAct_throws_when_not_in_draft()
        {
            var nc = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-07", Title = "T", Year = 2020, RetentionPeriodYears = 5
            });
            var act = _service.DraftAct("А-1", DateTime.Today, 1, new[] { nc.Id });
            _service.ApproveAct(act.Id, 5, DateTime.Today);

            Assert.Throws<InvalidOperationException>(() =>
                _service.ApproveAct(act.Id, 6, DateTime.Today));
        }

        [Fact]
        public void ExecuteAct_requires_approved_status_and_records_method()
        {
            var nc = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-07", Title = "T", Year = 2020, RetentionPeriodYears = 5
            });
            var act = _service.DraftAct("А-1", DateTime.Today, 1, new[] { nc.Id });

            Assert.Throws<InvalidOperationException>(() =>
                _service.ExecuteAct(act.Id, DateTime.Today, "шредер"));

            _service.ApproveAct(act.Id, 5, DateTime.Today);

            var executed = _service.ExecuteAct(act.Id, new DateTime(2026, 6, 1), "шредер");
            Assert.Equal(DestructionStatus.Executed, executed.Status);
            Assert.Equal("шредер", executed.DestructionMethod);
            Assert.Equal(new DateTime(2026, 6, 1), executed.ExecutedAt);

            var audit = _audit.GetLast();
            Assert.Equal(AuditActionType.DestructionActExecuted, audit.ActionType);
        }

        [Fact]
        public void CancelAct_allowed_from_draft_and_approved_but_not_executed()
        {
            var nc = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-07", Title = "T", Year = 2020, RetentionPeriodYears = 5
            });

            var a1 = _service.DraftAct("А-1", DateTime.Today, 1, new[] { nc.Id });
            var cancelled1 = _service.CancelAct(a1.Id, "ошибка в перечне");
            Assert.Equal(DestructionStatus.Cancelled, cancelled1.Status);
            Assert.Contains("ошибка в перечне", cancelled1.Notes);

            var a2 = _service.DraftAct("А-2", DateTime.Today, 1, new[] { nc.Id });
            _service.ApproveAct(a2.Id, 5, DateTime.Today);
            var cancelled2 = _service.CancelAct(a2.Id);
            Assert.Equal(DestructionStatus.Cancelled, cancelled2.Status);

            var a3 = _service.DraftAct("А-3", DateTime.Today, 1, new[] { nc.Id });
            _service.ApproveAct(a3.Id, 5, DateTime.Today);
            _service.ExecuteAct(a3.Id, DateTime.Today);
            Assert.Throws<InvalidOperationException>(() => _service.CancelAct(a3.Id));
        }

        // =========================================================
        // (3) ReportService DOCX outputs.
        // =========================================================

        [Fact]
        public void GenerateDestructionAct_creates_non_empty_docx()
        {
            var nc = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-07", Title = "Переписка по АХЧ", Year = 2020,
                RetentionPeriodYears = 5, Article = "ст. 19"
            });
            var act = _service.DraftAct("АКТ-2026-001", new DateTime(2026, 5, 1),
                draftedByEmployeeId: 1, caseIds: new[] { nc.Id });

            var tmp = Path.Combine(Path.GetTempPath(), $"phase19_act_{Guid.NewGuid():N}.docx");
            try
            {
                _reports.GenerateDestructionAct(act.Id, tmp);
                Assert.True(File.Exists(tmp));
                Assert.True(new FileInfo(tmp).Length > 0);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        [Fact]
        public void GenerateDestructionAct_throws_for_unknown_act()
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"phase19_act_{Guid.NewGuid():N}.docx");
            Assert.Throws<InvalidOperationException>(() => _reports.GenerateDestructionAct(99999, tmp));
        }

        [Theory]
        [InlineData(ArchiveResponseKind.Spravka)]
        [InlineData(ArchiveResponseKind.Vypiska)]
        [InlineData(ArchiveResponseKind.Kopiya)]
        public void GenerateArchiveResponse_creates_non_empty_docx(ArchiveResponseKind kind)
        {
            var req = new ArchiveRequest
            {
                Title = "Запрос о стаже",
                Correspondent = "Иванов И.И.",
                CreationDate = new DateTime(2026, 5, 1),
                RequestKind = ArchiveRequestKind.SocialLegal,
            };
            _documents.Add(req);

            var tmp = Path.Combine(Path.GetTempPath(), $"phase19_resp_{Guid.NewGuid():N}.docx");
            try
            {
                _reports.GenerateArchiveResponse(req.Id, kind, tmp);
                Assert.True(File.Exists(tmp));
                Assert.True(new FileInfo(tmp).Length > 0);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        [Fact]
        public void GenerateArchiveResponse_throws_for_non_archive_request_document()
        {
            var doc = new Document
            {
                Title = "Не архивный", CreationDate = DateTime.Today, RegistrationNumber = "ВХ-1"
            };
            _documents.Add(doc);
            var tmp = Path.Combine(Path.GetTempPath(), $"phase19_resp_{Guid.NewGuid():N}.docx");
            Assert.Throws<InvalidOperationException>(
                () => _reports.GenerateArchiveResponse(doc.Id, ArchiveResponseKind.Spravka, tmp));
        }
    }
}
