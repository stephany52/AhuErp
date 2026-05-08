using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Тесты Phase 14 / Improvement #10 — расширение модуля ИТО:
    /// каталог оборудования, журналы диагностики и ВКС, KPI-метрики.
    /// Используем in-memory реализации репозиториев, чтобы не зависеть от EF6.
    /// </summary>
    public class ItServicePhase14Tests
    {
        // ======================== Equipment ====================================

        [Fact]
        public void Equipment_add_assigns_id_and_persists_fields()
        {
            var repo = new InMemoryEquipmentRepository();
            var saved = repo.Add(new Equipment
            {
                InventoryNumber = "АХУ-ИТ-0001",
                Type = EquipmentType.Pc,
                Model = "HP ProDesk 400",
                SerialNumber = "SN-1",
                MacAddress = "AA:BB:CC:DD:EE:01",
                IpAddress = "10.0.0.21",
                Room = "К-204",
                Status = EquipmentStatus.Working,
            });

            Assert.True(saved.Id > 0);
            var fetched = repo.Get(saved.Id);
            Assert.NotNull(fetched);
            Assert.Equal("АХУ-ИТ-0001", fetched.InventoryNumber);
            Assert.Equal(EquipmentType.Pc, fetched.Type);
            Assert.Equal("10.0.0.21", fetched.IpAddress);
        }

        [Fact]
        public void Equipment_add_rejects_duplicate_inventory_number()
        {
            var repo = new InMemoryEquipmentRepository();
            repo.Add(new Equipment { InventoryNumber = "АХУ-ИТ-0001" });

            var ex = Assert.Throws<InvalidOperationException>(() =>
                repo.Add(new Equipment { InventoryNumber = "АХУ-ИТ-0001" }));
            Assert.Contains("уже зарегистрировано", ex.Message);
        }

        [Fact]
        public void Equipment_add_rejects_blank_inventory_number()
        {
            var repo = new InMemoryEquipmentRepository();
            Assert.Throws<ArgumentException>(() =>
                repo.Add(new Equipment { InventoryNumber = "  " }));
        }

        [Fact]
        public void Equipment_list_by_status_filters_correctly()
        {
            var repo = new InMemoryEquipmentRepository();
            repo.Add(new Equipment { InventoryNumber = "A1", Status = EquipmentStatus.Working });
            repo.Add(new Equipment { InventoryNumber = "A2", Status = EquipmentStatus.InRepair });
            repo.Add(new Equipment { InventoryNumber = "A3", Status = EquipmentStatus.Decommissioned });
            repo.Add(new Equipment { InventoryNumber = "A4", Status = EquipmentStatus.InRepair });

            var inRepair = repo.ListByStatus(EquipmentStatus.InRepair);
            Assert.Equal(2, inRepair.Count);
            Assert.All(inRepair, e => Assert.Equal(EquipmentStatus.InRepair, e.Status));
        }

        [Fact]
        public void Equipment_list_by_responsible_filters_correctly()
        {
            var repo = new InMemoryEquipmentRepository();
            repo.Add(new Equipment { InventoryNumber = "A1", ResponsibleEmployeeId = 7 });
            repo.Add(new Equipment { InventoryNumber = "A2", ResponsibleEmployeeId = 7 });
            repo.Add(new Equipment { InventoryNumber = "A3", ResponsibleEmployeeId = 9 });

            var owned = repo.ListByResponsible(7);
            Assert.Equal(2, owned.Count);
            Assert.All(owned, e => Assert.Equal(7, e.ResponsibleEmployeeId));
        }

        [Fact]
        public void Equipment_update_changes_existing_record()
        {
            var repo = new InMemoryEquipmentRepository();
            var e = repo.Add(new Equipment { InventoryNumber = "A1", Status = EquipmentStatus.Working });
            e.Status = EquipmentStatus.InRepair;
            repo.Update(e);

            Assert.Equal(EquipmentStatus.InRepair, repo.Get(e.Id).Status);
        }

        [Fact]
        public void Equipment_update_throws_for_unknown_id()
        {
            var repo = new InMemoryEquipmentRepository();
            Assert.Throws<InvalidOperationException>(() =>
                repo.Update(new Equipment { Id = 999, InventoryNumber = "X" }));
        }

        [Fact]
        public void Equipment_get_by_inventory_number_is_case_insensitive()
        {
            var repo = new InMemoryEquipmentRepository();
            repo.Add(new Equipment { InventoryNumber = "АХУ-ИТ-0001" });

            Assert.NotNull(repo.GetByInventoryNumber("аху-ит-0001"));
            Assert.Null(repo.GetByInventoryNumber("nope"));
        }

        // ======================== NetworkSegment ===============================

        [Fact]
        public void NetworkSegment_add_assigns_id_and_persists_fields()
        {
            var repo = new InMemoryNetworkSegmentRepository();
            var saved = repo.Add(new NetworkSegment
            {
                Name = "ADMIN-VLAN",
                Vlan = "10",
                IpRange = "10.0.10.0/24",
                SubnetMask = "255.255.255.0",
                Gateway = "10.0.10.1",
                Dns = "10.0.10.2",
            });

            Assert.True(saved.Id > 0);
            var fetched = repo.Get(saved.Id);
            Assert.Equal("10", fetched.Vlan);
            Assert.Equal("10.0.10.0/24", fetched.IpRange);
        }

        [Fact]
        public void NetworkSegment_list_returns_records_in_alphabetical_order()
        {
            var repo = new InMemoryNetworkSegmentRepository();
            repo.Add(new NetworkSegment { Name = "ZONE-B" });
            repo.Add(new NetworkSegment { Name = "ZONE-A" });
            repo.Add(new NetworkSegment { Name = "ZONE-C" });

            var list = repo.List();
            Assert.Equal(new[] { "ZONE-A", "ZONE-B", "ZONE-C" },
                         list.Select(s => s.Name).ToArray());
        }

        [Fact]
        public void NetworkSegment_delete_removes_record()
        {
            var repo = new InMemoryNetworkSegmentRepository();
            var s = repo.Add(new NetworkSegment { Name = "S1" });
            repo.Delete(s.Id);

            Assert.Null(repo.Get(s.Id));
            Assert.Empty(repo.List());
        }

        // ======================== VideoConference ==============================

        [Fact]
        public void VideoConference_add_persists_meeting_metadata()
        {
            var repo = new InMemoryVideoConferenceRepository();
            var saved = repo.Add(new VideoConference
            {
                Topic = "Совещание глав поселений",
                ScheduledAt = new DateTime(2026, 5, 12, 10, 0, 0),
                OrganizerId = 1,
                Platform = VideoConferencePlatform.Zoom,
                MeetingUrl = "https://zoom.example/abc",
                Participants = "Глава, Зам, Делопроизводитель",
            });

            Assert.True(saved.Id > 0);
            var fetched = repo.Get(saved.Id);
            Assert.Equal("Совещание глав поселений", fetched.Topic);
            Assert.Equal(VideoConferencePlatform.Zoom, fetched.Platform);
        }

        [Fact]
        public void VideoConference_list_in_range_filters_by_scheduled_at()
        {
            var repo = new InMemoryVideoConferenceRepository();
            var inRange = new DateTime(2026, 5, 12);
            var beforeRange = new DateTime(2026, 5, 1);
            var afterRange = new DateTime(2026, 6, 1);

            repo.Add(new VideoConference { Topic = "before", ScheduledAt = beforeRange, OrganizerId = 1 });
            repo.Add(new VideoConference { Topic = "in", ScheduledAt = inRange, OrganizerId = 1 });
            repo.Add(new VideoConference { Topic = "after", ScheduledAt = afterRange, OrganizerId = 1 });

            var month = repo.ListInRange(new DateTime(2026, 5, 1), new DateTime(2026, 6, 1));
            Assert.Equal(new[] { "before", "in" }, month.Select(v => v.Topic).OrderBy(s => s).ToArray());
        }

        [Fact]
        public void VideoConference_list_by_ticket_returns_only_linked()
        {
            var repo = new InMemoryVideoConferenceRepository();
            repo.Add(new VideoConference { Topic = "linked-1", OrganizerId = 1, TicketId = 100, ScheduledAt = DateTime.Now });
            repo.Add(new VideoConference { Topic = "free", OrganizerId = 1, TicketId = null, ScheduledAt = DateTime.Now });
            repo.Add(new VideoConference { Topic = "linked-2", OrganizerId = 1, TicketId = 100, ScheduledAt = DateTime.Now });

            var linked = repo.ListByTicket(100);
            Assert.Equal(2, linked.Count);
            Assert.All(linked, v => Assert.Equal(100, v.TicketId));
        }

        // ======================== Diagnostic entries ===========================

        [Fact]
        public void DiagnosticEntry_add_assigns_id_and_validates()
        {
            var repo = new InMemoryItTicketDiagnosticRepository();
            var saved = repo.Add(new ItTicketDiagnosticEntry
            {
                TicketId = 1,
                AuthorId = 7,
                Timestamp = new DateTime(2026, 5, 8, 12, 0, 0),
                Action = "Перезагрузил роутер",
                Category = "Сеть",
            });
            Assert.True(saved.Id > 0);
        }

        [Fact]
        public void DiagnosticEntry_add_rejects_blank_action()
        {
            var repo = new InMemoryItTicketDiagnosticRepository();
            Assert.Throws<ArgumentException>(() =>
                repo.Add(new ItTicketDiagnosticEntry
                {
                    TicketId = 1,
                    AuthorId = 7,
                    Action = "  ",
                }));
        }

        [Fact]
        public void DiagnosticEntry_add_rejects_missing_ticket()
        {
            var repo = new InMemoryItTicketDiagnosticRepository();
            Assert.Throws<ArgumentException>(() =>
                repo.Add(new ItTicketDiagnosticEntry
                {
                    TicketId = 0,
                    AuthorId = 7,
                    Action = "x",
                }));
        }

        [Fact]
        public void DiagnosticEntry_list_by_ticket_returns_chronological_order()
        {
            var repo = new InMemoryItTicketDiagnosticRepository();
            repo.Add(new ItTicketDiagnosticEntry
            {
                TicketId = 1,
                AuthorId = 7,
                Timestamp = new DateTime(2026, 5, 8, 12, 0, 0),
                Action = "Step 2",
            });
            repo.Add(new ItTicketDiagnosticEntry
            {
                TicketId = 1,
                AuthorId = 7,
                Timestamp = new DateTime(2026, 5, 8, 11, 0, 0),
                Action = "Step 1",
            });
            repo.Add(new ItTicketDiagnosticEntry
            {
                TicketId = 2,
                AuthorId = 7,
                Timestamp = new DateTime(2026, 5, 8, 13, 0, 0),
                Action = "Other ticket",
            });

            var entries = repo.ListByTicket(1);
            Assert.Equal(2, entries.Count);
            Assert.Equal("Step 1", entries[0].Action);
            Assert.Equal("Step 2", entries[1].Action);
        }

        // ======================== ItTicket new fields ==========================

        [Fact]
        public void ItTicket_defaults_are_set_for_phase14_fields()
        {
            var t = new ItTicket();
            Assert.Equal(ItTicketKind.HardwareRepair, t.Kind);
            Assert.False(t.IsSentToVendor);
            Assert.Null(t.AffectedEquipmentId);
            Assert.Null(t.CompletedAt);
            Assert.NotNull(t.DiagnosticEntries);
            Assert.Empty(t.DiagnosticEntries);
        }

        [Fact]
        public void ItTicket_kind_classifier_persists_in_in_memory_repository()
        {
            var docs = new InMemoryDocumentRepository();
            var t = new ItTicket
            {
                Title = "Установить 1С",
                Kind = ItTicketKind.SoftwareInstall,
                CreationDate = DateTime.Now,
                Deadline = DateTime.Now.AddDays(7),
                Status = DocumentStatus.New,
            };
            docs.Add(t);

            var fetched = (ItTicket)docs.GetById(t.Id);
            Assert.Equal(ItTicketKind.SoftwareInstall, fetched.Kind);
        }

        // ======================== KPI metrics ==================================

        [Fact]
        public void Metrics_empty_repository_returns_zero_snapshot()
        {
            var docs = new InMemoryDocumentRepository();
            var metrics = new ItServiceMetricsProvider(docs);

            var snapshot = metrics.Compute();
            Assert.Equal(0, snapshot.OpenCount);
            Assert.Equal(0, snapshot.InProgressCount);
            Assert.Equal(0, snapshot.OverdueCount);
            Assert.Equal(0, snapshot.SentToVendorCount);
            Assert.Equal(0, snapshot.CompletedCount);
            Assert.Null(snapshot.MeanTimeToResolve);
        }

        [Fact]
        public void Metrics_open_count_excludes_terminal_states()
        {
            var docs = new InMemoryDocumentRepository();
            var asOf = new DateTime(2026, 5, 8, 12, 0, 0);

            docs.Add(MakeTicket(1, DocumentStatus.New, asOf));
            docs.Add(MakeTicket(2, DocumentStatus.InProgress, asOf));
            docs.Add(MakeTicket(3, DocumentStatus.OnHold, asOf));
            docs.Add(MakeTicket(4, DocumentStatus.Completed, asOf, completedAt: asOf));
            docs.Add(MakeTicket(5, DocumentStatus.Cancelled, asOf));

            var snapshot = new ItServiceMetricsProvider(docs).Compute(asOf);

            Assert.Equal(3, snapshot.OpenCount);
            Assert.Equal(1, snapshot.CompletedCount);
        }

        [Fact]
        public void Metrics_in_progress_includes_in_progress_status_and_sent_to_vendor()
        {
            var docs = new InMemoryDocumentRepository();
            var asOf = new DateTime(2026, 5, 8, 12, 0, 0);

            docs.Add(MakeTicket(1, DocumentStatus.New, asOf));
            docs.Add(MakeTicket(2, DocumentStatus.InProgress, asOf));
            // OnHold + IsSentToVendor — заявка передана в сервис, считается «в работе».
            docs.Add(MakeTicket(3, DocumentStatus.OnHold, asOf, isSentToVendor: true));

            var snapshot = new ItServiceMetricsProvider(docs).Compute(asOf);

            Assert.Equal(3, snapshot.OpenCount);
            Assert.Equal(2, snapshot.InProgressCount);
            Assert.Equal(1, snapshot.SentToVendorCount);
        }

        [Fact]
        public void Metrics_overdue_count_uses_deadline_against_asof()
        {
            var docs = new InMemoryDocumentRepository();
            var asOf = new DateTime(2026, 5, 8, 12, 0, 0);

            // Просрочена.
            var overdue = MakeTicket(1, DocumentStatus.InProgress, asOf.AddDays(-10));
            overdue.Deadline = asOf.AddDays(-2);
            docs.Add(overdue);

            // Ещё не просрочена.
            var ok = MakeTicket(2, DocumentStatus.InProgress, asOf.AddDays(-1));
            ok.Deadline = asOf.AddDays(3);
            docs.Add(ok);

            // Просроченную закрытую заявку в overdue не считаем.
            var closedOverdue = MakeTicket(3, DocumentStatus.Completed, asOf.AddDays(-10), completedAt: asOf);
            closedOverdue.Deadline = asOf.AddDays(-5);
            docs.Add(closedOverdue);

            var snapshot = new ItServiceMetricsProvider(docs).Compute(asOf);
            Assert.Equal(1, snapshot.OverdueCount);
        }

        [Fact]
        public void Metrics_mttr_is_average_of_completed_durations()
        {
            var docs = new InMemoryDocumentRepository();
            var asOf = new DateTime(2026, 5, 8, 18, 0, 0);

            // 2 часа.
            var t1 = MakeTicket(1, DocumentStatus.Completed, asOf.AddHours(-4),
                                completedAt: asOf.AddHours(-2));
            // 4 часа.
            var t2 = MakeTicket(2, DocumentStatus.Completed, asOf.AddHours(-6),
                                completedAt: asOf.AddHours(-2));
            docs.Add(t1);
            docs.Add(t2);

            var snapshot = new ItServiceMetricsProvider(docs).Compute(asOf);

            Assert.NotNull(snapshot.MeanTimeToResolve);
            Assert.Equal(TimeSpan.FromHours(3), snapshot.MeanTimeToResolve);
            Assert.Equal(2, snapshot.CompletedCount);
        }

        [Fact]
        public void Metrics_mttr_ignores_completed_without_completed_at()
        {
            var docs = new InMemoryDocumentRepository();
            var asOf = new DateTime(2026, 5, 8, 18, 0, 0);

            // Закрыт, но без CompletedAt — в MTTR не попадает.
            docs.Add(MakeTicket(1, DocumentStatus.Completed, asOf.AddHours(-4)));
            // Закрыт с CompletedAt = 2 часа.
            docs.Add(MakeTicket(2, DocumentStatus.Completed, asOf.AddHours(-4),
                                completedAt: asOf.AddHours(-2)));

            var snapshot = new ItServiceMetricsProvider(docs).Compute(asOf);

            Assert.Equal(2, snapshot.CompletedCount);
            Assert.NotNull(snapshot.MeanTimeToResolve);
            Assert.Equal(TimeSpan.FromHours(2), snapshot.MeanTimeToResolve);
        }

        [Fact]
        public void Metrics_mttr_is_null_when_no_closed_tickets()
        {
            var docs = new InMemoryDocumentRepository();
            var asOf = new DateTime(2026, 5, 8, 18, 0, 0);
            docs.Add(MakeTicket(1, DocumentStatus.InProgress, asOf.AddHours(-1)));

            var snapshot = new ItServiceMetricsProvider(docs).Compute(asOf);
            Assert.Null(snapshot.MeanTimeToResolve);
        }

        [Fact]
        public void Metrics_throws_when_documents_repo_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new ItServiceMetricsProvider(null));
        }

        // ---- helpers ---------------------------------------------------------

        private static ItTicket MakeTicket(int id,
                                           DocumentStatus status,
                                           DateTime created,
                                           bool isSentToVendor = false,
                                           DateTime? completedAt = null)
        {
            return new ItTicket
            {
                Id = id,
                Title = "T" + id,
                CreationDate = created,
                Deadline = created.AddDays(7),
                Status = status,
                IsSentToVendor = isSentToVendor,
                CompletedAt = completedAt,
            };
        }
    }
}
