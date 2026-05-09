using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using ClosedXML.Excel;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 15 / Improvement #12 — журналы регистрации (ГСМ, ОТ/ПБ,
    /// инвентаризации, передача дел в архив, договоры). Проверяем:
    /// (1) расчёт ГСМ в <see cref="VehicleTrip"/>,
    /// (2) фильтрацию in-memory репозиториев,
    /// (3) структуру XLSX-выгрузок <see cref="ReportService"/>.
    /// </summary>
    public class Phase15RegistrationJournalsTests : IDisposable
    {
        private readonly InMemoryInventoryRepository _inventory = new InMemoryInventoryRepository();
        private readonly InMemoryDocumentRepository _documents = new InMemoryDocumentRepository();
        private readonly InMemoryTaskRepository _taskRepo = new InMemoryTaskRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly InMemoryNomenclatureRepository _nomenclature = new InMemoryNomenclatureRepository();
        private readonly InMemoryVehicleRepository _vehicles = new InMemoryVehicleRepository();
        private readonly AuditService _audit;
        private readonly TaskService _tasks;
        private readonly ReportService _service;
        private readonly string _workdir;

        public Phase15RegistrationJournalsTests()
        {
            _audit = new AuditService(_auditRepo);
            _tasks = new TaskService(_taskRepo, _documents, _audit);
            _service = new ReportService(_inventory, _documents, _tasks, _taskRepo,
                _nomenclature, _vehicles, _audit);
            _workdir = Path.Combine(Path.GetTempPath(), "AhuErpTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workdir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_workdir, recursive: true); } catch { /* best-effort */ }
        }

        // =========================================================
        // VehicleTrip — расчёт расхода ГСМ и пробега.
        // =========================================================

        [Fact]
        public void VehicleTrip_DistanceKm_returns_difference_when_both_odometers_set()
        {
            var trip = new VehicleTrip { OdometerStart = 12000, OdometerEnd = 12345 };
            Assert.Equal(345, trip.DistanceKm);
        }

        [Fact]
        public void VehicleTrip_DistanceKm_returns_null_when_odometer_missing()
        {
            Assert.Null(new VehicleTrip { OdometerStart = 100 }.DistanceKm);
            Assert.Null(new VehicleTrip { OdometerEnd = 100 }.DistanceKm);
            Assert.Null(new VehicleTrip().DistanceKm);
        }

        [Fact]
        public void VehicleTrip_FuelUsedLiters_uses_consumption_norm_and_distance()
        {
            var vehicle = new Vehicle { Model = "ГАЗ-3221", LicensePlate = "А001АА", FuelConsumptionPer100Km = 12m };
            var trip = new VehicleTrip
            {
                Vehicle = vehicle,
                OdometerStart = 50_000,
                OdometerEnd = 50_250, // 250 km
            };
            // 250 km * 12 л/100 = 30 л.
            Assert.Equal(30.0m, trip.FuelUsedLiters);
        }

        [Fact]
        public void VehicleTrip_FuelUsedLiters_returns_null_without_norm_or_odometer()
        {
            var v = new Vehicle { Model = "x", LicensePlate = "y", FuelConsumptionPer100Km = 0m };
            Assert.Null(new VehicleTrip { Vehicle = v, OdometerStart = 0, OdometerEnd = 100 }.FuelUsedLiters);
            Assert.Null(new VehicleTrip { Vehicle = null, OdometerStart = 0, OdometerEnd = 100 }.FuelUsedLiters);
            Assert.Null(new VehicleTrip { Vehicle = new Vehicle { FuelConsumptionPer100Km = 10 }, OdometerStart = null, OdometerEnd = 100 }.FuelUsedLiters);
        }

        [Fact]
        public void VehicleTrip_FuelUsedLiters_returns_null_when_odometer_decreases()
        {
            var v = new Vehicle { Model = "x", LicensePlate = "y", FuelConsumptionPer100Km = 10m };
            var trip = new VehicleTrip { Vehicle = v, OdometerStart = 200, OdometerEnd = 100 };
            Assert.Null(trip.FuelUsedLiters);
        }

        // =========================================================
        // InMemorySafetyBriefingRepository.
        // =========================================================

        [Fact]
        public void SafetyBriefingRepository_filters_by_period_and_kind()
        {
            var repo = new InMemorySafetyBriefingRepository();
            repo.Add(new SafetyBriefing { BriefingDate = new DateTime(2026, 1, 10), Kind = BriefingKind.Initial, Topic = "Вводный" });
            repo.Add(new SafetyBriefing { BriefingDate = new DateTime(2026, 2, 10), Kind = BriefingKind.Recurring, Topic = "Повторный" });
            repo.Add(new SafetyBriefing { BriefingDate = new DateTime(2026, 3, 10), Kind = BriefingKind.Recurring, Topic = "Повторный 2" });

            var inFebMarch = repo.List(new DateTime(2026, 2, 1), new DateTime(2026, 3, 31), null);
            Assert.Equal(2, inFebMarch.Count);

            var recurring = repo.List(null, null, BriefingKind.Recurring);
            Assert.Equal(2, recurring.Count);
            Assert.All(recurring, b => Assert.Equal(BriefingKind.Recurring, b.Kind));

            var initialOnly = repo.List(null, null, BriefingKind.Initial);
            Assert.Single(initialOnly);
            Assert.Equal("Вводный", initialOnly[0].Topic);
        }

        [Fact]
        public void SafetyBriefingRepository_supports_update_and_remove()
        {
            var repo = new InMemorySafetyBriefingRepository();
            var b = new SafetyBriefing
            {
                BriefingDate = new DateTime(2026, 4, 1),
                Kind = BriefingKind.Initial,
                Topic = "Без изменений",
            };
            repo.Add(b);
            Assert.NotEqual(0, b.Id);

            b.SignatureConfirmed = true;
            b.Topic = "Обновлено";
            repo.Update(b);
            var fetched = repo.GetById(b.Id);
            Assert.True(fetched.SignatureConfirmed);
            Assert.Equal("Обновлено", fetched.Topic);

            repo.Remove(b.Id);
            Assert.Null(repo.GetById(b.Id));
        }

        // =========================================================
        // InMemoryInventarizationRepository.
        // =========================================================

        [Fact]
        public void InventarizationRepository_persists_discrepancies_and_filters_by_scope()
        {
            var repo = new InMemoryInventarizationRepository();
            var inv = new Inventarization
            {
                StartDate = new DateTime(2026, 5, 1),
                EndDate = new DateTime(2026, 5, 5),
                Scope = InventarizationScope.Inventory,
                ScopeDescription = "Склад ТМЦ",
                CommissionMembers = "Иванов;Петров",
            };
            inv.Discrepancies.Add(new InventarizationDiscrepancy { ItemName = "Бумага", ExpectedQuantity = 10, ActualQuantity = 8 });
            inv.Discrepancies.Add(new InventarizationDiscrepancy { ItemName = "Картриджи", ExpectedQuantity = 5, ActualQuantity = 6 });
            repo.Add(inv);

            repo.Add(new Inventarization
            {
                StartDate = new DateTime(2026, 6, 1),
                Scope = InventarizationScope.FixedAssets,
                ScopeDescription = "ОС в кабинете 12",
            });

            var inventoryOnly = repo.List(null, null, InventarizationScope.Inventory);
            Assert.Single(inventoryOnly);
            var loaded = repo.GetById(inv.Id);
            Assert.Equal(2, loaded.Discrepancies.Count);
            Assert.All(loaded.Discrepancies, d => Assert.NotEqual(0, d.Id));
            Assert.All(loaded.Discrepancies, d => Assert.Equal(loaded.Id, d.InventarizationId));

            // Дельты считаются нативно моделью.
            var paper = loaded.Discrepancies.Single(d => d.ItemName == "Бумага");
            Assert.Equal(-2m, paper.Delta);
        }

        // =========================================================
        // InMemoryArchiveTransferRepository.
        // =========================================================

        [Fact]
        public void ArchiveTransferRepository_filters_by_period_and_case()
        {
            var repo = new InMemoryArchiveTransferRepository();
            repo.Add(new ArchiveTransfer { NomenclatureCaseId = 1, TransferDate = new DateTime(2026, 1, 15), ArchiveCode = "01-08-2026" });
            repo.Add(new ArchiveTransfer { NomenclatureCaseId = 2, TransferDate = new DateTime(2026, 2, 15), ArchiveCode = "02-01-2026" });
            repo.Add(new ArchiveTransfer { NomenclatureCaseId = 1, TransferDate = new DateTime(2026, 3, 1), ArchiveCode = "01-09-2026" });

            var inJan = repo.List(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));
            Assert.Single(inJan);
            Assert.Equal("01-08-2026", inJan[0].ArchiveCode);

            var caseOne = repo.ListByCase(1);
            Assert.Equal(2, caseOne.Count);
            Assert.All(caseOne, t => Assert.Equal(1, t.NomenclatureCaseId));
        }

        // =========================================================
        // ReportService.ExportFuelLog.
        // =========================================================

        [Fact]
        public void ExportFuelLog_writes_xlsx_with_distance_and_fuel_used()
        {
            var vehicle = new Vehicle
            {
                Model = "ГАЗ-3221",
                LicensePlate = "А001АА777",
                FuelType = FuelType.Diesel,
                FuelConsumptionPer100Km = 12m,
            };
            var trip = new VehicleTrip
            {
                Vehicle = vehicle,
                StartDate = new DateTime(2026, 6, 1, 8, 0, 0),
                EndDate = new DateTime(2026, 6, 1, 18, 0, 0),
                ActualStart = new DateTime(2026, 6, 1, 8, 5, 0),
                ActualEnd = new DateTime(2026, 6, 1, 17, 30, 0),
                DriverName = "Иванов И.И.",
                Route = "Гараж — Администрация — Гараж",
                OdometerStart = 100_000,
                OdometerEnd = 100_250,
                FuelIssuedLiters = 35m,
            };

            var path = Path.Combine(_workdir, "fuel.xlsx");
            _service.ExportFuelLog(new[] { trip }, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), path);

            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Equal("Журнал учёта ГСМ", sheet.Cell(1, 1).GetString());
                Assert.Equal("Дата", sheet.Cell(4, 1).GetString());
                Assert.Equal("01.06.2026", sheet.Cell(5, 1).GetString());
                Assert.Contains("ГАЗ-3221", sheet.Cell(5, 2).GetString());
                Assert.Equal("Дизель", sheet.Cell(5, 3).GetString());
                Assert.Equal("Иванов И.И.", sheet.Cell(5, 4).GetString());
                Assert.Equal("Гараж — Администрация — Гараж", sheet.Cell(5, 5).GetString());
                Assert.Equal("100000", sheet.Cell(5, 6).GetString());
                Assert.Equal("100250", sheet.Cell(5, 7).GetString());
                Assert.Equal("250", sheet.Cell(5, 8).GetString());
                Assert.Equal("35", sheet.Cell(5, 9).GetString());
                Assert.Equal("30", sheet.Cell(5, 10).GetString());
            }
        }

        [Fact]
        public void ExportFuelLog_handles_empty_collection_and_missing_odometers()
        {
            var path = Path.Combine(_workdir, "fuel-empty.xlsx");
            _service.ExportFuelLog(Array.Empty<VehicleTrip>(),
                new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), path);
            Assert.True(new FileInfo(path).Length > 0);

            var path2 = Path.Combine(_workdir, "fuel-incomplete.xlsx");
            var trip = new VehicleTrip
            {
                StartDate = new DateTime(2026, 1, 5),
                Vehicle = new Vehicle { Model = "M", LicensePlate = "P", FuelConsumptionPer100Km = 10m },
                DriverName = "Сидоров",
                // одометр не заполнен: расход и пробег должны вывестись как «—».
            };
            _service.ExportFuelLog(new[] { trip },
                new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), path2);
            using (var wb = new XLWorkbook(path2))
            {
                var sheet = wb.Worksheet(1);
                Assert.Equal("—", sheet.Cell(5, 6).GetString()); // odo start
                Assert.Equal("—", sheet.Cell(5, 8).GetString()); // distance
                Assert.Equal("—", sheet.Cell(5, 10).GetString()); // fuel used
            }
        }

        [Fact]
        public void ExportFuelLog_validates_arguments()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _service.ExportFuelLog(null, DateTime.MinValue, DateTime.MaxValue, "x.xlsx"));
            Assert.Throws<ArgumentException>(() =>
                _service.ExportFuelLog(Array.Empty<VehicleTrip>(), DateTime.MinValue, DateTime.MaxValue, ""));
        }

        // =========================================================
        // ReportService.ExportSafetyBriefingsJournal.
        // =========================================================

        [Fact]
        public void ExportSafetyBriefingsJournal_writes_xlsx_with_signed_flag()
        {
            var trainee = new Employee { FullName = "Иванов И.И.", Email = "i@i" };
            var instructor = new Employee { FullName = "Петров П.П.", Email = "p@p" };
            var briefings = new List<SafetyBriefing>
            {
                new SafetyBriefing
                {
                    BriefingDate = new DateTime(2026, 1, 15),
                    Kind = BriefingKind.Initial,
                    Topic = "Вводный инструктаж",
                    TraineeEmployee = trainee,
                    InstructorEmployee = instructor,
                    SignatureConfirmed = true,
                    Notes = "Без замечаний",
                },
                new SafetyBriefing
                {
                    BriefingDate = new DateTime(2026, 2, 15),
                    Kind = BriefingKind.Recurring,
                    Topic = "Повторный",
                    TraineeEmployee = trainee,
                    InstructorEmployee = instructor,
                    SignatureConfirmed = false,
                },
            };

            var path = Path.Combine(_workdir, "ot.xlsx");
            _service.ExportSafetyBriefingsJournal(briefings, path);

            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Contains("Журнал инструктажей", sheet.Cell(1, 1).GetString());
                Assert.Equal("Дата", sheet.Cell(4, 1).GetString());
                Assert.Equal("15.01.2026", sheet.Cell(5, 1).GetString());
                Assert.Equal("Вводный", sheet.Cell(5, 2).GetString());
                Assert.Equal("Вводный инструктаж", sheet.Cell(5, 3).GetString());
                Assert.Equal("Иванов И.И.", sheet.Cell(5, 4).GetString());
                Assert.Equal("Петров П.П.", sheet.Cell(5, 5).GetString());
                Assert.Equal("Подписано", sheet.Cell(5, 6).GetString());
                Assert.Equal("Без замечаний", sheet.Cell(5, 7).GetString());
                Assert.Equal("Не подписано", sheet.Cell(6, 6).GetString());
            }
        }

        [Fact]
        public void ExportSafetyBriefingsJournal_validates_arguments()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _service.ExportSafetyBriefingsJournal(null, "x.xlsx"));
            Assert.Throws<ArgumentException>(() =>
                _service.ExportSafetyBriefingsJournal(Array.Empty<SafetyBriefing>(), "  "));
        }

        // =========================================================
        // ReportService.ExportInventarizationsJournal.
        // =========================================================

        [Fact]
        public void ExportInventarizationsJournal_writes_xlsx_with_discrepancy_count()
        {
            var chairman = new Employee { FullName = "Сидоров С.С.", Email = "s@s" };
            var inv = new Inventarization
            {
                StartDate = new DateTime(2026, 4, 1),
                EndDate = new DateTime(2026, 4, 5),
                Scope = InventarizationScope.Inventory,
                ScopeDescription = "Склад ТМЦ — кабинет 12",
                Chairman = chairman,
                CommissionMembers = "Сидоров;Кузнецов",
            };
            inv.Discrepancies.Add(new InventarizationDiscrepancy { ItemName = "Бумага", ExpectedQuantity = 10, ActualQuantity = 7 });
            inv.Discrepancies.Add(new InventarizationDiscrepancy { ItemName = "Картриджи", ExpectedQuantity = 5, ActualQuantity = 6 });

            var path = Path.Combine(_workdir, "inv.xlsx");
            _service.ExportInventarizationsJournal(new[] { inv }, path);

            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Equal("Журнал инвентаризаций", sheet.Cell(1, 1).GetString());
                Assert.Equal("01.04.2026", sheet.Cell(5, 1).GetString());
                Assert.Equal("05.04.2026", sheet.Cell(5, 2).GetString());
                Assert.Equal("Склад ТМЦ", sheet.Cell(5, 3).GetString());
                Assert.Equal("Склад ТМЦ — кабинет 12", sheet.Cell(5, 4).GetString());
                Assert.Equal("Сидоров С.С.", sheet.Cell(5, 5).GetString());
                Assert.Equal("Сидоров;Кузнецов", sheet.Cell(5, 6).GetString());
                Assert.Equal(2, sheet.Cell(5, 7).GetValue<int>());
            }
        }

        [Fact]
        public void ExportInventarizationsJournal_validates_arguments()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _service.ExportInventarizationsJournal(null, "x.xlsx"));
            Assert.Throws<ArgumentException>(() =>
                _service.ExportInventarizationsJournal(Array.Empty<Inventarization>(), null));
        }

        // =========================================================
        // ReportService.ExportArchiveTransferJournal.
        // =========================================================

        [Fact]
        public void ExportArchiveTransferJournal_writes_xlsx_with_archive_code()
        {
            var caseRef = new NomenclatureCase { Index = "01-08", Title = "Переписка", Year = 2025, RetentionPeriodYears = 5 };
            var transferer = new Employee { FullName = "Иванов И.И.", Email = "i@i" };
            var acceptor = new Employee { FullName = "Архивист А.А.", Email = "a@a" };
            var transfer = new ArchiveTransfer
            {
                NomenclatureCase = caseRef,
                TransferDate = new DateTime(2026, 6, 30),
                TransferredBy = transferer,
                AcceptedBy = acceptor,
                ArchiveCode = "01-08/2025-01",
                RetentionYears = 75,
                Notes = "Дело передано полностью",
            };

            var path = Path.Combine(_workdir, "archive.xlsx");
            _service.ExportArchiveTransferJournal(new[] { transfer }, path);

            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Equal("Журнал передачи дел в архив", sheet.Cell(1, 1).GetString());
                Assert.Equal("30.06.2026", sheet.Cell(5, 1).GetString());
                Assert.Equal("01-08", sheet.Cell(5, 2).GetString());
                Assert.Equal("Переписка", sheet.Cell(5, 3).GetString());
                Assert.Equal("01-08/2025-01", sheet.Cell(5, 4).GetString());
                Assert.Equal("Иванов И.И.", sheet.Cell(5, 5).GetString());
                Assert.Equal("Архивист А.А.", sheet.Cell(5, 6).GetString());
                Assert.Equal("75 лет", sheet.Cell(5, 8).GetString());
                Assert.Equal("Дело передано полностью", sheet.Cell(5, 9).GetString());
            }
        }

        [Fact]
        public void ExportArchiveTransferJournal_marks_zero_retention_as_permanent()
        {
            var caseRef = new NomenclatureCase { Index = "ПК-1", Title = "Постоянное хранение", Year = 2025, RetentionPeriodYears = 0 };
            var transfer = new ArchiveTransfer
            {
                NomenclatureCase = caseRef,
                TransferDate = new DateTime(2026, 7, 1),
                ArchiveCode = "ПК-1/2025",
                RetentionYears = 0,
            };

            var path = Path.Combine(_workdir, "archive-perm.xlsx");
            _service.ExportArchiveTransferJournal(new[] { transfer }, path);

            using (var wb = new XLWorkbook(path))
            {
                Assert.Equal("Постоянно", wb.Worksheet(1).Cell(5, 8).GetString());
            }
        }

        // =========================================================
        // ReportService.ExportContractsJournal.
        // =========================================================

        [Fact]
        public void ExportContractsJournal_uses_registration_journal_template()
        {
            var contract = new Document
            {
                Title = "Договор поставки бумаги",
                Type = DocumentType.Office,
                Direction = DocumentDirection.Incoming,
                CreationDate = new DateTime(2026, 2, 1),
                Deadline = new DateTime(2026, 12, 31),
                RegistrationNumber = "ДОГ-1/2026",
                RegistrationDate = new DateTime(2026, 2, 5),
                Correspondent = "ООО Поставщик",
            };

            var path = Path.Combine(_workdir, "contracts.xlsx");
            _service.ExportContractsJournal(new[] { contract },
                new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), path);

            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Contains("Журнал договоров", sheet.Cell(1, 1).GetString());
                // ExportRegistrationJournal расставляет шапку в строке 3 (см. Phase 8).
                // Проверим, что регистрационный номер договора попал в данные.
                bool found = false;
                for (int r = 1; r <= 20; r++)
                {
                    for (int c = 1; c <= 8; c++)
                    {
                        if (sheet.Cell(r, c).GetString() == "ДОГ-1/2026") { found = true; break; }
                    }
                    if (found) break;
                }
                Assert.True(found, "Регистрационный номер договора должен присутствовать в журнале.");
            }
        }

        [Fact]
        public void ExportContractsJournal_validates_arguments()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _service.ExportContractsJournal(null, DateTime.MinValue, DateTime.MaxValue, "x.xlsx"));
            Assert.Throws<ArgumentException>(() =>
                _service.ExportContractsJournal(Array.Empty<Document>(), DateTime.MinValue, DateTime.MaxValue, ""));
        }
    }
}
