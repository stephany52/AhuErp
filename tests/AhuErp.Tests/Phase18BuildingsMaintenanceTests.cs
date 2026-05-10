using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 18 / Improvement #15 — здания, помещения, заявки на эксплуатационные
    /// работы и реестр основных средств.
    /// Покрытие:
    /// (1) Repository-инварианты (уникальность Name / (BuildingId, Number) /
    ///     InventoryNumber, фильтры).
    /// (2) <see cref="BuildingService"/> — CRUD + аудит, FK-проверка для Room.
    /// (3) <see cref="MaintenanceService"/> — стейт-машина Open → InProgress →
    ///     Completed | Cancelled, валидация, нотификация назначенному.
    /// </summary>
    public class Phase18BuildingsMaintenanceTests
    {
        private readonly InMemoryBuildingRepository _buildings = new InMemoryBuildingRepository();
        private readonly InMemoryRoomRepository _rooms = new InMemoryRoomRepository();
        private readonly InMemoryMaintenanceRequestRepository _requests = new InMemoryMaintenanceRequestRepository();
        private readonly InMemoryFixedAssetRepository _assets = new InMemoryFixedAssetRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly InMemoryNotificationRepository _notificationRepo = new InMemoryNotificationRepository();
        private readonly InMemoryEmployeeRepository _employees = new InMemoryEmployeeRepository();
        private readonly InMemoryTaskRepository _tasks = new InMemoryTaskRepository();
        private readonly AuditService _audit;
        private readonly NotificationService _notifications;
        private readonly BuildingService _buildingService;
        private readonly MaintenanceService _maintenanceService;

        public Phase18BuildingsMaintenanceTests()
        {
            _audit = new AuditService(_auditRepo);
            _notifications = new NotificationService(_notificationRepo, _employees, _tasks, _audit);
            _buildingService = new BuildingService(_buildings, _rooms, _audit);
            _maintenanceService = new MaintenanceService(_requests, _audit, _notifications);
        }

        // =========================================================
        // Building / Room repositories.
        // =========================================================

        [Fact]
        public void Building_Add_assigns_id_and_persists()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус", Address = "ул. Ленина 1" });
            Assert.True(b.Id > 0);
            Assert.Equal(b, _buildings.Get(b.Id));
        }

        [Fact]
        public void Building_Add_rejects_duplicate_name()
        {
            _buildings.Add(new Building { Name = "Главный корпус" });
            Assert.Throws<InvalidOperationException>(
                () => _buildings.Add(new Building { Name = "Главный корпус" }));
        }

        [Fact]
        public void Building_Add_rejects_empty_name()
        {
            Assert.Throws<ArgumentException>(
                () => _buildings.Add(new Building { Name = "" }));
        }

        [Fact]
        public void Building_GetByName_returns_match_or_null()
        {
            _buildings.Add(new Building { Name = "Корпус А" });
            Assert.NotNull(_buildings.GetByName("Корпус А"));
            Assert.Null(_buildings.GetByName("Корпус Б"));
        }

        [Fact]
        public void Building_List_orders_alphabetically()
        {
            _buildings.Add(new Building { Name = "Б-корпус" });
            _buildings.Add(new Building { Name = "А-корпус" });
            _buildings.Add(new Building { Name = "В-корпус" });

            var list = _buildings.List();
            Assert.Equal(new[] { "А-корпус", "Б-корпус", "В-корпус" },
                list.Select(b => b.Name).ToArray());
        }

        [Fact]
        public void Room_Add_rejects_duplicate_number_within_building()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            _rooms.Add(new Room { BuildingId = b.Id, Number = "101" });
            Assert.Throws<InvalidOperationException>(() =>
                _rooms.Add(new Room { BuildingId = b.Id, Number = "101" }));
        }

        [Fact]
        public void Room_Add_allows_same_number_in_different_buildings()
        {
            var b1 = _buildings.Add(new Building { Name = "Корпус А" });
            var b2 = _buildings.Add(new Building { Name = "Корпус Б" });
            _rooms.Add(new Room { BuildingId = b1.Id, Number = "101" });
            var r2 = _rooms.Add(new Room { BuildingId = b2.Id, Number = "101" });
            Assert.True(r2.Id > 0);
        }

        [Fact]
        public void Room_ListByBuilding_returns_only_matching_rooms()
        {
            var b1 = _buildings.Add(new Building { Name = "Корпус А" });
            var b2 = _buildings.Add(new Building { Name = "Корпус Б" });
            _rooms.Add(new Room { BuildingId = b1.Id, Number = "101" });
            _rooms.Add(new Room { BuildingId = b1.Id, Number = "102" });
            _rooms.Add(new Room { BuildingId = b2.Id, Number = "201" });

            var inA = _rooms.ListByBuilding(b1.Id);
            Assert.Equal(2, inA.Count);
            Assert.All(inA, r => Assert.Equal(b1.Id, r.BuildingId));
        }

        [Fact]
        public void Room_ListByPurpose_filters_correctly()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            _rooms.Add(new Room { BuildingId = b.Id, Number = "101", Purpose = RoomPurpose.Office });
            _rooms.Add(new Room { BuildingId = b.Id, Number = "102", Purpose = RoomPurpose.Storage });
            _rooms.Add(new Room { BuildingId = b.Id, Number = "201", Purpose = RoomPurpose.Office });

            var offices = _rooms.ListByPurpose(RoomPurpose.Office);
            Assert.Equal(2, offices.Count);
        }

        // =========================================================
        // BuildingService — CRUD + audit.
        // =========================================================

        [Fact]
        public void BuildingService_RegisterBuilding_writes_audit()
        {
            var b = _buildingService.RegisterBuilding(
                new Building { Name = "Главный корпус" }, actorId: 42);

            Assert.True(b.Id > 0);
            var entries = _auditRepo.Query(new AuditQueryFilter
            {
                ActionType = AuditActionType.BuildingCreated,
            });
            Assert.Single(entries);
            Assert.Equal(42, entries[0].UserId);
            Assert.Equal(b.Id, entries[0].EntityId);
            Assert.Equal("Building", entries[0].EntityType);
        }

        [Fact]
        public void BuildingService_AddRoom_throws_when_building_missing()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _buildingService.AddRoom(
                    new Room { BuildingId = 999, Number = "101" }, actorId: 1));
        }

        [Fact]
        public void BuildingService_UpdateRoom_writes_audit()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            var room = _rooms.Add(new Room { BuildingId = b.Id, Number = "101" });

            room.Notes = "Перенесена секретная зона";
            _buildingService.UpdateRoom(room, actorId: 7);

            var entries = _auditRepo.Query(new AuditQueryFilter
            {
                ActionType = AuditActionType.RoomUpdated,
            });
            Assert.Single(entries);
            Assert.Equal(room.Id, entries[0].EntityId);
        }

        // =========================================================
        // MaintenanceService — лайфцикл.
        // =========================================================

        [Fact]
        public void MaintenanceService_CreateRequest_sets_status_open_and_writes_audit()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            var request = _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b.Id,
                RequesterEmployeeId = 5,
                Kind = MaintenanceKind.Plumbing,
                Priority = MaintenancePriority.High,
                Description = "Течёт кран",
            }, actorId: 5);

            Assert.True(request.Id > 0);
            Assert.Equal(MaintenanceStatus.Open, request.Status);
            Assert.Null(request.CompletedAt);

            var entries = _auditRepo.Query(new AuditQueryFilter
            {
                ActionType = AuditActionType.MaintenanceRequestCreated,
            });
            Assert.Single(entries);
        }

        [Fact]
        public void MaintenanceService_CreateRequest_sets_default_registration_date()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            var before = DateTime.Now;
            var request = _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b.Id,
                RequesterEmployeeId = 1,
                Description = "Тест",
            }, actorId: 1);
            var after = DateTime.Now;

            Assert.InRange(request.RegistrationDate, before.AddSeconds(-1), after.AddSeconds(1));
        }

        [Fact]
        public void MaintenanceService_Assign_transitions_open_to_inprogress()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            var request = _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b.Id,
                RequesterEmployeeId = 5,
                Description = "Заменить лампу",
            }, actorId: 5);

            var assigned = _maintenanceService.Assign(request.Id,
                assigneeEmployeeId: 11, actorId: 1);

            Assert.Equal(MaintenanceStatus.InProgress, assigned.Status);
            Assert.Equal(11, assigned.AssigneeEmployeeId);

            var notes = _notificationRepo.ListByRecipient(11, unreadOnly: false);
            Assert.Single(notes);
            Assert.Contains($"#{request.Id}", notes[0].Body);
        }

        [Fact]
        public void MaintenanceService_Assign_throws_for_completed_request()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            var request = _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b.Id,
                RequesterEmployeeId = 5,
                Description = "Ремонт",
            }, actorId: 5);
            _maintenanceService.Complete(request.Id, "Сделано", actorId: 1);

            Assert.Throws<InvalidOperationException>(() =>
                _maintenanceService.Assign(request.Id, 11, actorId: 1));
        }

        [Fact]
        public void MaintenanceService_Assign_validates_assignee_id()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            var request = _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b.Id,
                RequesterEmployeeId = 1,
                Description = "Тест",
            }, actorId: 1);

            Assert.Throws<ArgumentException>(() =>
                _maintenanceService.Assign(request.Id, 0, actorId: 1));
            Assert.Throws<ArgumentException>(() =>
                _maintenanceService.Assign(request.Id, -3, actorId: 1));
        }

        [Fact]
        public void MaintenanceService_Complete_sets_terminal_status_and_resolution()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            var request = _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b.Id,
                RequesterEmployeeId = 5,
                Description = "Ремонт",
            }, actorId: 5);
            _maintenanceService.Assign(request.Id, 11, actorId: 1);

            var now = new DateTime(2026, 6, 1, 12, 0, 0);
            var completed = _maintenanceService.Complete(request.Id,
                "Заменён картридж", actorId: 11, now: now);

            Assert.Equal(MaintenanceStatus.Completed, completed.Status);
            Assert.Equal("Заменён картридж", completed.Resolution);
            Assert.Equal(now, completed.CompletedAt);
        }

        [Fact]
        public void MaintenanceService_Complete_requires_resolution()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            var request = _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b.Id,
                RequesterEmployeeId = 1,
                Description = "Тест",
            }, actorId: 1);

            Assert.Throws<ArgumentException>(() =>
                _maintenanceService.Complete(request.Id, "", actorId: 1));
            Assert.Throws<ArgumentException>(() =>
                _maintenanceService.Complete(request.Id, null, actorId: 1));
        }

        [Fact]
        public void MaintenanceService_Complete_throws_for_already_terminal()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            var request = _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b.Id,
                RequesterEmployeeId = 1,
                Description = "Тест",
            }, actorId: 1);
            _maintenanceService.Cancel(request.Id, "Передумали", actorId: 1);

            Assert.Throws<InvalidOperationException>(() =>
                _maintenanceService.Complete(request.Id, "Сделано", actorId: 1));
        }

        [Fact]
        public void MaintenanceService_Cancel_sets_terminal_with_reason()
        {
            var b = _buildings.Add(new Building { Name = "Главный корпус" });
            var request = _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b.Id,
                RequesterEmployeeId = 1,
                Description = "Тест",
            }, actorId: 1);

            var cancelled = _maintenanceService.Cancel(request.Id,
                "Дубль заявки #42", actorId: 1);

            Assert.Equal(MaintenanceStatus.Cancelled, cancelled.Status);
            Assert.Contains("Дубль заявки", cancelled.Resolution);
            Assert.NotNull(cancelled.CompletedAt);
        }

        [Fact]
        public void MaintenanceService_ListRequests_filters_by_status_and_building()
        {
            var b1 = _buildings.Add(new Building { Name = "Корпус А" });
            var b2 = _buildings.Add(new Building { Name = "Корпус Б" });

            var r1 = _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b1.Id, RequesterEmployeeId = 1,
                Description = "A1",
            }, actorId: 1);
            _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b1.Id, RequesterEmployeeId = 1,
                Description = "A2",
            }, actorId: 1);
            var r3 = _maintenanceService.CreateRequest(new MaintenanceRequest
            {
                BuildingId = b2.Id, RequesterEmployeeId = 1,
                Description = "B1",
            }, actorId: 1);
            _maintenanceService.Complete(r1.Id, "ok", actorId: 1);

            var openInA = _maintenanceService.ListRequests(null, null, b1.Id, MaintenanceStatus.Open);
            Assert.Single(openInA);

            var allInB = _maintenanceService.ListRequests(null, null, b2.Id, null);
            Assert.Single(allInB);
            Assert.Equal(r3.Id, allInB[0].Id);
        }

        // =========================================================
        // FixedAsset repository.
        // =========================================================

        [Fact]
        public void FixedAsset_Add_rejects_duplicate_inventory_number()
        {
            _assets.Add(new FixedAsset { InventoryNumber = "ОС-0001", Name = "Принтер" });
            Assert.Throws<InvalidOperationException>(() =>
                _assets.Add(new FixedAsset { InventoryNumber = "ОС-0001", Name = "Сканер" }));
        }

        [Fact]
        public void FixedAsset_Add_rejects_empty_inventory_number()
        {
            Assert.Throws<ArgumentException>(() =>
                _assets.Add(new FixedAsset { InventoryNumber = "", Name = "Тест" }));
        }

        [Fact]
        public void FixedAsset_GetByInventoryNumber_returns_match_or_null()
        {
            _assets.Add(new FixedAsset { InventoryNumber = "ОС-0001", Name = "Принтер" });
            Assert.NotNull(_assets.GetByInventoryNumber("ОС-0001"));
            Assert.Null(_assets.GetByInventoryNumber("ОС-9999"));
        }

        [Fact]
        public void FixedAsset_ListByCategory_filters()
        {
            _assets.Add(new FixedAsset
            {
                InventoryNumber = "ОС-0001", Name = "Стол",
                Category = FixedAssetCategory.Furniture,
            });
            _assets.Add(new FixedAsset
            {
                InventoryNumber = "ОС-0002", Name = "ПК",
                Category = FixedAssetCategory.OfficeEquipment,
            });
            _assets.Add(new FixedAsset
            {
                InventoryNumber = "ОС-0003", Name = "Ноутбук",
                Category = FixedAssetCategory.OfficeEquipment,
            });

            var office = _assets.ListByCategory(FixedAssetCategory.OfficeEquipment);
            Assert.Equal(2, office.Count);
        }

        [Fact]
        public void FixedAsset_ListByStatus_filters()
        {
            _assets.Add(new FixedAsset
            {
                InventoryNumber = "ОС-0001", Name = "Стол",
                Status = FixedAssetStatus.InUse,
            });
            _assets.Add(new FixedAsset
            {
                InventoryNumber = "ОС-0002", Name = "Старый сервер",
                Status = FixedAssetStatus.Decommissioned,
            });

            var inUse = _assets.ListByStatus(FixedAssetStatus.InUse);
            Assert.Single(inUse);
            Assert.Equal("Стол", inUse[0].Name);
        }

        [Fact]
        public void FixedAsset_ListByResponsible_filters()
        {
            _assets.Add(new FixedAsset
            {
                InventoryNumber = "ОС-0001", Name = "Стол",
                ResponsibleEmployeeId = 5,
            });
            _assets.Add(new FixedAsset
            {
                InventoryNumber = "ОС-0002", Name = "Стул",
                ResponsibleEmployeeId = 7,
            });
            _assets.Add(new FixedAsset
            {
                InventoryNumber = "ОС-0003", Name = "Лампа",
                ResponsibleEmployeeId = 5,
            });

            var ofIvanov = _assets.ListByResponsible(5);
            Assert.Equal(2, ofIvanov.Count);
        }

        [Fact]
        public void FixedAsset_ListByBuilding_filters()
        {
            var b1 = _buildings.Add(new Building { Name = "Корпус А" });
            var b2 = _buildings.Add(new Building { Name = "Корпус Б" });
            _assets.Add(new FixedAsset
            {
                InventoryNumber = "ОС-0001", Name = "Стол",
                BuildingId = b1.Id,
            });
            _assets.Add(new FixedAsset
            {
                InventoryNumber = "ОС-0002", Name = "Принтер",
                BuildingId = b2.Id,
            });

            var inA = _assets.ListByBuilding(b1.Id);
            Assert.Single(inA);
            Assert.Equal("Стол", inA[0].Name);
        }
    }
}
