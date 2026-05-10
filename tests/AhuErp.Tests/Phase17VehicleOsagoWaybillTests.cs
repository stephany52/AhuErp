using System;
using System.IO;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 17 / Improvement #14 — паспортные данные ТС, ОСАГО / ТО,
    /// печать путевого листа.
    /// Покрытие:
    /// (1) <see cref="VehicleMaintenanceService"/> — уведомления за 30 дней
    ///     до истечения ОСАГО / ТО, плановое ТО по пробегу, идемпотентность.
    /// (2) <see cref="ReportService.GenerateTripWaybill"/> — DOCX-форма №3
    ///     для легкового и №4-С для грузового ТС.
    /// </summary>
    public class Phase17VehicleOsagoWaybillTests : IDisposable
    {
        private readonly InMemoryVehicleRepository _vehicles = new InMemoryVehicleRepository();
        private readonly InMemoryEmployeeRepository _employees = new InMemoryEmployeeRepository();
        private readonly InMemoryNotificationRepository _notificationRepo = new InMemoryNotificationRepository();
        private readonly InMemoryDocumentRepository _documents = new InMemoryDocumentRepository();
        private readonly InMemoryInventoryRepository _inventory = new InMemoryInventoryRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly NotificationService _notifications;
        private readonly VehicleMaintenanceService _service;
        private readonly AuditService _audit;
        private readonly ReportService _reports;
        private readonly string _workdir;

        public Phase17VehicleOsagoWaybillTests()
        {
            _audit = new AuditService(_auditRepo);
            _notifications = new NotificationService(_notificationRepo,
                _employees, new InMemoryTaskRepository(), _audit);
            _service = new VehicleMaintenanceService(_vehicles, _notifications, _employees, _notificationRepo);
            _reports = new ReportService(_inventory, _documents, tasks: null, taskRepo: null,
                nomenclature: null, _vehicles, _audit);
            _workdir = Path.Combine(Path.GetTempPath(), "AhuErpTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workdir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_workdir, recursive: true); } catch { /* best-effort */ }
        }

        // =========================================================
        // VehicleMaintenanceService — OSAGO / ТО / пробег.
        // =========================================================

        [Fact]
        public void CheckExpiringDocuments_creates_osago_alert_within_window()
        {
            SeedRecipient(EmployeeRole.WarehouseManager, "Иванов И.И.");
            var now = new DateTime(2026, 6, 1);
            _vehicles.AddVehicle(new Vehicle
            {
                Make = "ГАЗ", Model = "3221", LicensePlate = "А001АА64",
                OsagoExpiry = now.AddDays(15), // в окне 30 дней
            });

            var created = _service.CheckExpiringDocuments(now, daysAhead: 30, kmAhead: 1000);

            Assert.Single(created);
            Assert.Equal(NotificationKind.VehicleOsagoExpiringSoon, created[0].Kind);
            Assert.Contains("ОСАГО", created[0].Title);
            Assert.Contains("А001АА64", created[0].Body);
        }

        [Fact]
        public void CheckExpiringDocuments_does_not_create_when_outside_window()
        {
            SeedRecipient(EmployeeRole.WarehouseManager, "Иванов И.И.");
            var now = new DateTime(2026, 6, 1);
            _vehicles.AddVehicle(new Vehicle
            {
                Make = "ГАЗ", Model = "3221", LicensePlate = "А001АА64",
                OsagoExpiry = now.AddDays(60), // вне окна
                TechInspectionExpiry = now.AddDays(120),
            });

            var created = _service.CheckExpiringDocuments(now, daysAhead: 30, kmAhead: 1000);
            Assert.Empty(created);
        }

        [Fact]
        public void CheckExpiringDocuments_creates_tech_inspection_alert_within_window()
        {
            SeedRecipient(EmployeeRole.FleetManager, "Петров П.П.");
            var now = new DateTime(2026, 6, 1);
            _vehicles.AddVehicle(new Vehicle
            {
                Make = "Toyota", Model = "Camry", LicensePlate = "В002ВВ64",
                TechInspectionExpiry = now.AddDays(7),
            });

            var created = _service.CheckExpiringDocuments(now, daysAhead: 30, kmAhead: 1000);

            Assert.Single(created);
            Assert.Equal(NotificationKind.VehicleTechInspectionExpiringSoon, created[0].Kind);
            Assert.Contains("ТО", created[0].Title);
            Assert.Contains("В002ВВ64", created[0].Body);
        }

        [Fact]
        public void CheckExpiringDocuments_creates_maintenance_alert_when_odometer_close()
        {
            SeedRecipient(EmployeeRole.Admin, "Админов А.А.");
            var now = new DateTime(2026, 6, 1);
            _vehicles.AddVehicle(new Vehicle
            {
                Make = "ВАЗ", Model = "Vesta", LicensePlate = "Е005ЕЕ64",
                OdometerCurrent = 49_500,
                NextMaintenanceOdometer = 50_000, // осталось 500 км — внутри 1000
            });

            var created = _service.CheckExpiringDocuments(now, daysAhead: 30, kmAhead: 1000);

            Assert.Single(created);
            Assert.Equal(NotificationKind.VehicleMaintenanceDueSoon, created[0].Kind);
            Assert.Contains("Плановое ТО", created[0].Title);
            Assert.Contains("Е005ЕЕ64", created[0].Body);
        }

        [Fact]
        public void CheckExpiringDocuments_does_not_create_when_odometer_far()
        {
            SeedRecipient(EmployeeRole.Admin, "Админов А.А.");
            _vehicles.AddVehicle(new Vehicle
            {
                Make = "ВАЗ", Model = "Vesta", LicensePlate = "Е005ЕЕ64",
                OdometerCurrent = 30_000,
                NextMaintenanceOdometer = 50_000, // осталось 20000 км
            });

            var created = _service.CheckExpiringDocuments(DateTime.Now,
                daysAhead: 30, kmAhead: 1000);
            Assert.Empty(created);
        }

        [Fact]
        public void CheckExpiringDocuments_is_idempotent_for_same_day()
        {
            SeedRecipient(EmployeeRole.WarehouseManager, "Иванов И.И.");
            var now = new DateTime(2026, 6, 1);
            _vehicles.AddVehicle(new Vehicle
            {
                Make = "ГАЗ", Model = "3221", LicensePlate = "А001АА64",
                OsagoExpiry = now.AddDays(10),
            });

            _service.CheckExpiringDocuments(now);
            var second = _service.CheckExpiringDocuments(now);

            Assert.Empty(second);
        }

        [Fact]
        public void CheckExpiringDocuments_distributes_to_all_eligible_roles()
        {
            SeedRecipient(EmployeeRole.WarehouseManager, "Иванов И.И.");
            SeedRecipient(EmployeeRole.FleetManager, "Петров П.П.");
            SeedRecipient(EmployeeRole.Admin, "Админов А.А.");
            SeedRecipient(EmployeeRole.TechSupport, "Иной С.С."); // не должен получить
            SeedRecipient(EmployeeRole.Manager, "Управ У.У."); // не должен получить

            var now = new DateTime(2026, 6, 1);
            _vehicles.AddVehicle(new Vehicle
            {
                Make = "ГАЗ", Model = "3221", LicensePlate = "А001АА64",
                OsagoExpiry = now.AddDays(10),
            });

            var created = _service.CheckExpiringDocuments(now);

            Assert.Equal(3, created.Count);
            Assert.All(created, n => Assert.Equal(NotificationKind.VehicleOsagoExpiringSoon, n.Kind));
        }

        [Fact]
        public void CheckExpiringDocuments_throws_on_negative_window()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.CheckExpiringDocuments(DateTime.Now, daysAhead: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.CheckExpiringDocuments(DateTime.Now, kmAhead: -1));
        }

        [Fact]
        public void CheckExpiringDocuments_returns_empty_when_no_eligible_recipients()
        {
            // Только TechSupport — не входит в список.
            SeedRecipient(EmployeeRole.TechSupport, "Иной С.С.");
            _vehicles.AddVehicle(new Vehicle
            {
                Make = "ГАЗ", Model = "3221", LicensePlate = "А001АА64",
                OsagoExpiry = DateTime.Now.AddDays(10),
            });

            var created = _service.CheckExpiringDocuments(DateTime.Now);
            Assert.Empty(created);
        }

        // =========================================================
        // ReportService.GenerateTripWaybill — DOCX №3 / №4-С.
        // =========================================================

        [Fact]
        public void GenerateTripWaybill_passenger_writes_form3_docx()
        {
            var vehicle = SeedVehicle(VehicleClass.Passenger,
                make: "Toyota", model: "Camry", plate: "В002ВВ64",
                year: 2020, vin: "JT123456789012345", consumption: 9.5m);
            var trip = SeedTrip(vehicle,
                driver: "Сидоров С.С.",
                route: "Гараж — администрация — Гараж",
                passengers: "Глава района, Зам. главы",
                odoStart: 50_000, odoEnd: 50_120,
                start: new DateTime(2026, 6, 1, 8, 0, 0),
                end: new DateTime(2026, 6, 1, 18, 0, 0),
                fuelIssued: 15m);

            var path = Path.Combine(_workdir, "waybill_passenger.docx");
            _reports.GenerateTripWaybill(trip.Id, path);

            Assert.True(File.Exists(path));
            var text = ExtractDocxText(path);
            Assert.Contains("ПУТЕВОЙ ЛИСТ ЛЕГКОВОГО АВТОМОБИЛЯ", text);
            Assert.Contains("Форма №3", text);
            Assert.Contains("Toyota", text);
            Assert.Contains("Camry", text);
            Assert.Contains("В002ВВ64", text);
            Assert.Contains("JT123456789012345", text);
            Assert.Contains("Сидоров С.С.", text);
            Assert.Contains("Гараж — администрация — Гараж", text);
            Assert.Contains("Глава района, Зам. главы", text);
            Assert.Contains("120", text); // distanceKm
            Assert.DoesNotContain("ВЫПОЛНЕНИЕ ЗАДАНИЯ (для грузового ТС)", text);
        }

        [Fact]
        public void GenerateTripWaybill_truck_writes_form4s_docx()
        {
            var vehicle = SeedVehicle(VehicleClass.Truck,
                make: "ГАЗ", model: "3309", plate: "У777УУ64",
                year: 2018, vin: "X9F123456789ABCDE", consumption: 18m);
            var trip = SeedTrip(vehicle,
                driver: "Кузнецов К.К.",
                route: "Гараж — Склад №1 — Школа №3 — Гараж",
                passengers: null,
                odoStart: 100_000, odoEnd: 100_200,
                start: new DateTime(2026, 6, 1, 7, 0, 0),
                end: new DateTime(2026, 6, 1, 17, 0, 0),
                fuelIssued: 45m);

            var path = Path.Combine(_workdir, "waybill_truck.docx");
            _reports.GenerateTripWaybill(trip.Id, path);

            Assert.True(File.Exists(path));
            var text = ExtractDocxText(path);
            Assert.Contains("ПУТЕВОЙ ЛИСТ ГРУЗОВОГО АВТОМОБИЛЯ", text);
            Assert.Contains("Форма №4-С", text);
            Assert.Contains("ВЫПОЛНЕНИЕ ЗАДАНИЯ (для грузового ТС)", text);
            Assert.Contains("Заказчик / грузоотправитель", text);
            Assert.Contains("Пункт погрузки", text);
            Assert.Contains("Наименование груза", text);
            Assert.Contains("Кузнецов К.К.", text);
        }

        [Fact]
        public void GenerateTripWaybill_throws_when_trip_missing()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _reports.GenerateTripWaybill(tripId: 999, Path.Combine(_workdir, "missing.docx")));
        }

        [Fact]
        public void GenerateTripWaybill_validates_arguments()
        {
            Assert.Throws<ArgumentException>(() => _reports.GenerateTripWaybill(1, ""));
            Assert.Throws<ArgumentException>(() => _reports.GenerateTripWaybill(1, null));
        }

        [Fact]
        public void GenerateTripWaybill_handles_missing_optional_data()
        {
            // Минимальные данные: только Vehicle + поездка без VIN, года, одометра, маршрута.
            var vehicle = SeedVehicle(VehicleClass.Passenger,
                make: null, model: "ВАЗ", plate: "А001АА64",
                year: 0, vin: null, consumption: 0m);
            var trip = SeedTrip(vehicle,
                driver: null, route: null, passengers: null,
                odoStart: null, odoEnd: null,
                start: new DateTime(2026, 6, 1, 8, 0, 0),
                end: new DateTime(2026, 6, 1, 18, 0, 0),
                fuelIssued: null);

            var path = Path.Combine(_workdir, "waybill_minimal.docx");
            _reports.GenerateTripWaybill(trip.Id, path);

            Assert.True(File.Exists(path));
            var text = ExtractDocxText(path);
            // «—» ставится вместо отсутствующих значений (Make, VIN, маршрут, водитель).
            Assert.Contains("—", text);
            Assert.Contains("ВАЗ", text);
        }

        // =========================================================
        // Helpers.
        // =========================================================

        private void SeedRecipient(EmployeeRole role, string name)
        {
            var emp = new Employee
            {
                Id = _employees.ListAll().Count + 1,
                FullName = name,
                Role = role,
                PasswordHash = "x",
            };
            _employees.Add(emp);
        }

        private Vehicle SeedVehicle(VehicleClass vehicleClass, string make, string model,
                                    string plate, int year, string vin, decimal consumption)
        {
            var v = new Vehicle
            {
                VehicleClass = vehicleClass,
                Make = make,
                Model = model,
                LicensePlate = plate,
                Year = year,
                Vin = vin,
                FuelConsumptionPer100Km = consumption,
                FuelType = FuelType.Petrol,
            };
            _vehicles.AddVehicle(v);
            return v;
        }

        private VehicleTrip SeedTrip(Vehicle vehicle, string driver, string route,
                                     string passengers,
                                     int? odoStart, int? odoEnd,
                                     DateTime start, DateTime end,
                                     decimal? fuelIssued)
        {
            var trip = new VehicleTrip
            {
                VehicleId = vehicle.Id,
                Vehicle = vehicle,
                DriverName = driver,
                Route = route,
                PassengerNames = passengers,
                OdometerStart = odoStart,
                OdometerEnd = odoEnd,
                FuelIssuedLiters = fuelIssued,
                StartDate = start,
                EndDate = end,
                ActualStart = start,
                ActualEnd = end,
            };
            _vehicles.AddTrip(trip);
            return trip;
        }

        private static string ExtractDocxText(string path)
        {
            using (var doc = WordprocessingDocument.Open(path, isEditable: false))
            {
                var sb = new System.Text.StringBuilder();
                foreach (var p in doc.MainDocumentPart.Document.Body.Descendants<W.Text>())
                {
                    sb.AppendLine(p.Text);
                }
                return sb.ToString();
            }
        }
    }
}
