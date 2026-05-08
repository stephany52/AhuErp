using System;
using AhuErp.Core.Models;
using AhuErp.Core.Services;

namespace AhuErp.UI.Infrastructure
{
    /// <summary>
    /// Заполняет in-memory репозитории демонстрационными данными для Phase 2.
    /// В production будет заменено на реальный <c>AhuDbContext</c>.
    /// </summary>
    public static class DemoDataSeeder
    {
        public const string DefaultPassword = "password";

        public static void Seed(InMemoryEmployeeRepository employees,
                                InMemoryDocumentRepository documents,
                                IPasswordHasher hasher)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            if (hasher == null) throw new ArgumentNullException(nameof(hasher));

            var hash = hasher.Hash(DefaultPassword);

            var admin = new Employee
            {
                Id = 1,
                FullName = "Администратор МКУ АХУ БМР",
                Position = "Администратор информационной системы",
                Role = EmployeeRole.Admin,
                PasswordHash = hash
            };
            var manager = new Employee
            {
                Id = 2,
                FullName = "Стерликов Дмитрий Николаевич",
                Position = "Руководитель службы по информационно-техническому обеспечению",
                Role = EmployeeRole.Manager,
                PasswordHash = hash
            };
            var archivist = new Employee
            {
                Id = 3,
                FullName = "Бурдина Галина Николаевна",
                Position = "Начальник архивного отдела",
                Role = EmployeeRole.Archivist,
                PasswordHash = hash
            };
            var tech = new Employee
            {
                Id = 4,
                FullName = "Дорофеев Артем Валерьевич",
                Position = "Специалист-техник по компьютерным сетям и системам",
                Role = EmployeeRole.TechSupport,
                PasswordHash = hash
            };
            var warehouse = new Employee
            {
                Id = 5,
                FullName = "Зайченко Татьяна Александровна",
                Position = "Специалист-техник по компьютерным сетям и системам",
                Role = EmployeeRole.WarehouseManager,
                PasswordHash = hash
            };
            var clerk = new Employee
            {
                Id = 6,
                FullName = "Иванова Светлана Петровна",
                Position = "Делопроизводитель отдела делопроизводства",
                Role = EmployeeRole.Clerk,
                PasswordHash = hash
            };
            var hr = new Employee
            {
                Id = 7,
                FullName = "Петрова Анна Викторовна",
                Position = "Специалист отдела учёта/отчётности/кадрового обеспечения",
                Role = EmployeeRole.HRAdmin,
                PasswordHash = hash
            };
            var deputy = new Employee
            {
                Id = 8,
                FullName = "Кузнецов Игорь Александрович",
                Position = "Заместитель руководителя",
                Role = EmployeeRole.DeputyHead,
                PasswordHash = hash
            };

            employees.Add(admin);
            employees.Add(manager);
            employees.Add(archivist);
            employees.Add(tech);
            employees.Add(warehouse);
            employees.Add(clerk);
            employees.Add(hr);
            employees.Add(deputy);

            var now = DateTime.Now;

            documents.Add(new Document
            {
                Type = DocumentType.Incoming,
                Title = "Входящее письмо Правительства Саратовской области",
                CreationDate = now.AddDays(-10),
                Deadline = now.AddDays(5),
                Status = DocumentStatus.InProgress,
                AssignedEmployeeId = manager.Id
            });
            documents.Add(new Document
            {
                Type = DocumentType.Internal,
                Title = "Проект распоряжения администрации БМР по делопроизводству",
                CreationDate = now.AddDays(-3),
                Deadline = now.AddDays(2),
                Status = DocumentStatus.New,
                AssignedEmployeeId = warehouse.Id
            });

            var archive = new ArchiveRequest
            {
                Title = "Архивная справка о стаже и заработной плате",
                Status = DocumentStatus.InProgress,
                HasPassportScan = true,
                HasWorkBookScan = false,
                RequestKind = ArchiveRequestKind.SocialLegal,
                AssignedEmployeeId = archivist.Id
            };
            archive.InitializeDeadline(now.AddDays(-25));
            documents.Add(archive);

            documents.Add(new ItTicket
            {
                Title = "Профилактика оргтехники в кабинете № 3",
                AffectedEquipment = "Рабочая станция и принтер, ул. Советская, 178 каб. 3",
                CreationDate = now.AddDays(-2),
                Deadline = now.AddDays(3),
                Status = DocumentStatus.InProgress,
                AssignedEmployeeId = tech.Id
            });
        }

        public static void SeedInventory(InMemoryInventoryRepository inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));

            inventory.AddItem(new InventoryItem
            {
                Name = "Бумага А4 500 л.",
                Category = InventoryCategory.Stationery,
                Unit = "пачка",
                MinimumBalance = 10,
                TotalQuantity = 40
            });
            inventory.AddItem(new InventoryItem
            {
                Name = "Ручка шариковая синяя",
                Category = InventoryCategory.Stationery,
                Unit = "шт.",
                MinimumBalance = 30,
                TotalQuantity = 120
            });
            inventory.AddItem(new InventoryItem
            {
                Name = "Картридж HP 59A",
                Category = InventoryCategory.IT_Equipment,
                Unit = "шт.",
                MinimumBalance = 2,
                TotalQuantity = 6
            });
            inventory.AddItem(new InventoryItem
            {
                Name = "Средство для уборки помещений 5 л",
                Category = InventoryCategory.Cleaning_Supplies,
                Unit = "канистра",
                MinimumBalance = 2,
                TotalQuantity = 8
            });
        }

        public static void SeedFleet(InMemoryVehicleRepository vehicles,
                                     InMemoryDocumentRepository documents)
        {
            if (vehicles == null) throw new ArgumentNullException(nameof(vehicles));
            if (documents == null) throw new ArgumentNullException(nameof(documents));

            vehicles.AddVehicle(new Vehicle
            {
                Model = "Lada Largus",
                LicensePlate = "А123БВ 64",
                CurrentStatus = VehicleStatus.Available
            });
            vehicles.AddVehicle(new Vehicle
            {
                Model = "ГАЗель NEXT",
                LicensePlate = "В777ТТ 64",
                CurrentStatus = VehicleStatus.Available
            });
            vehicles.AddVehicle(new Vehicle
            {
                Model = "УАЗ Патриот",
                LicensePlate = "Е111КХ 64",
                CurrentStatus = VehicleStatus.Maintenance
            });

            var now = DateTime.Now;
            documents.Add(new Document
            {
                Title = "Заявка на транспорт: Советская, 178 → архивный отдел на ул. Авиаторов",
                Type = DocumentType.Fleet,
                CreationDate = now.AddDays(-1),
                Deadline = now.AddDays(2),
                Status = DocumentStatus.InProgress
            });
            documents.Add(new Document
            {
                Title = "Заявка на транспорт: доставка документов организаций-источников комплектования",
                Type = DocumentType.Fleet,
                CreationDate = now,
                Deadline = now.AddDays(7),
                Status = DocumentStatus.New
            });
        }
    }
}
