namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 18 / Improvement #15 — эксплуатация зданий и реестр основных средств.
    /// <list type="bullet">
    ///   <item><description><c>Buildings</c> — здания учреждения (наименование,
    ///     адрес, общая площадь, этажность, год ввода, ответственный сотрудник).</description></item>
    ///   <item><description><c>Rooms</c> — помещения в зданиях (номер уникален в пределах
    ///     одного здания, этаж, площадь, функциональное назначение,
    ///     ответственный сотрудник).</description></item>
    ///   <item><description><c>MaintenanceRequests</c> — заявки на эксплуатационные работы
    ///     (электрика, сантехника, климат, плотницкие, уборка, ремонт, СКС/слаботочка).
    ///     Жизненный цикл независим от <c>DocumentStatus</c> — заявка не проходит
    ///     согласование/подписание; опционально может быть привязана к РКК.</description></item>
    ///   <item><description><c>FixedAssets</c> — реестр основных средств
    ///     (бухгалтерское представление, инвентарный номер по форме ОС-6,
    ///     стоимость приобретения, балансовая стоимость, МОЛ, дата списания
    ///     и документ-основание).</description></item>
    /// </list>
    /// Схема создаётся внешним <c>scripts/create-db.sql</c>; миграция документирует
    /// изменения и поддерживает совместимость с EF6 Add-Migration.
    /// </summary>
    /// <remarks>
    /// Снимок модели в .resx сгенерирован вручную как заглушка. После
    /// возврата в среду VS / EF6 PowerShell миграцию следует пересобрать
    /// командой <c>Add-Migration AddBuildingsMaintenancePhase18 -Force</c>,
    /// чтобы зафиксировать корректный EDM-снимок для последующих миграций.
    /// </remarks>
    public partial class AddBuildingsMaintenancePhase18 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Buildings",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    Name = c.String(nullable: false, maxLength: 128),
                    Address = c.String(maxLength: 256),
                    TotalAreaSqm = c.Decimal(nullable: false, precision: 10, scale: 2, defaultValue: 0m),
                    FloorCount = c.Int(nullable: false, defaultValue: 0),
                    CommissionedYear = c.Int(nullable: false, defaultValue: 0),
                    ResponsibleEmployeeId = c.Int(),
                    Notes = c.String(maxLength: 2048),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.ResponsibleEmployeeId)
                .Index(t => t.Name, unique: true)
                .Index(t => t.ResponsibleEmployeeId);

            CreateTable(
                "dbo.Rooms",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    BuildingId = c.Int(nullable: false),
                    Number = c.String(nullable: false, maxLength: 32),
                    Floor = c.Int(nullable: false, defaultValue: 0),
                    AreaSqm = c.Decimal(nullable: false, precision: 10, scale: 2, defaultValue: 0m),
                    Purpose = c.Int(nullable: false, defaultValue: 1),
                    ResponsibleEmployeeId = c.Int(),
                    Notes = c.String(maxLength: 1024),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Buildings", t => t.BuildingId, cascadeDelete: true)
                .ForeignKey("dbo.Employees", t => t.ResponsibleEmployeeId)
                .Index(t => new { t.BuildingId, t.Number }, unique: true, name: "IX_Rooms_Building_Number")
                .Index(t => t.ResponsibleEmployeeId);

            CreateTable(
                "dbo.MaintenanceRequests",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    RegistrationDate = c.DateTime(nullable: false),
                    BuildingId = c.Int(nullable: false),
                    RoomId = c.Int(),
                    RequesterEmployeeId = c.Int(nullable: false),
                    Kind = c.Int(nullable: false, defaultValue: 0),
                    Priority = c.Int(nullable: false, defaultValue: 1),
                    Status = c.Int(nullable: false, defaultValue: 0),
                    Description = c.String(nullable: false, maxLength: 2048),
                    AssigneeEmployeeId = c.Int(),
                    CompletedAt = c.DateTime(),
                    Resolution = c.String(maxLength: 2048),
                    LinkedDocumentId = c.Int(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Buildings", t => t.BuildingId)
                .ForeignKey("dbo.Rooms", t => t.RoomId)
                .ForeignKey("dbo.Employees", t => t.RequesterEmployeeId)
                .ForeignKey("dbo.Employees", t => t.AssigneeEmployeeId)
                .ForeignKey("dbo.Documents", t => t.LinkedDocumentId)
                .Index(t => t.BuildingId)
                .Index(t => t.RoomId)
                .Index(t => t.RequesterEmployeeId)
                .Index(t => t.AssigneeEmployeeId)
                .Index(t => t.LinkedDocumentId)
                .Index(t => t.Status)
                .Index(t => t.RegistrationDate);

            CreateTable(
                "dbo.FixedAssets",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    InventoryNumber = c.String(nullable: false, maxLength: 64),
                    Name = c.String(nullable: false, maxLength: 256),
                    Category = c.Int(nullable: false, defaultValue: 0),
                    Status = c.Int(nullable: false, defaultValue: 0),
                    AcquisitionDate = c.DateTime(),
                    AcquisitionCost = c.Decimal(nullable: false, precision: 18, scale: 2, defaultValue: 0m),
                    BookValue = c.Decimal(nullable: false, precision: 18, scale: 2, defaultValue: 0m),
                    BuildingId = c.Int(),
                    RoomId = c.Int(),
                    ResponsibleEmployeeId = c.Int(),
                    DecommissionedAt = c.DateTime(),
                    DecommissionDocumentId = c.Int(),
                    Notes = c.String(maxLength: 2048),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Buildings", t => t.BuildingId)
                .ForeignKey("dbo.Rooms", t => t.RoomId)
                .ForeignKey("dbo.Employees", t => t.ResponsibleEmployeeId)
                .ForeignKey("dbo.Documents", t => t.DecommissionDocumentId)
                .Index(t => t.InventoryNumber, unique: true)
                .Index(t => t.BuildingId)
                .Index(t => t.RoomId)
                .Index(t => t.ResponsibleEmployeeId)
                .Index(t => t.DecommissionDocumentId)
                .Index(t => t.Category)
                .Index(t => t.Status);
        }

        public override void Down()
        {
            DropForeignKey("dbo.FixedAssets", "DecommissionDocumentId", "dbo.Documents");
            DropForeignKey("dbo.FixedAssets", "ResponsibleEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.FixedAssets", "RoomId", "dbo.Rooms");
            DropForeignKey("dbo.FixedAssets", "BuildingId", "dbo.Buildings");
            DropIndex("dbo.FixedAssets", new[] { "Status" });
            DropIndex("dbo.FixedAssets", new[] { "Category" });
            DropIndex("dbo.FixedAssets", new[] { "DecommissionDocumentId" });
            DropIndex("dbo.FixedAssets", new[] { "ResponsibleEmployeeId" });
            DropIndex("dbo.FixedAssets", new[] { "RoomId" });
            DropIndex("dbo.FixedAssets", new[] { "BuildingId" });
            DropIndex("dbo.FixedAssets", new[] { "InventoryNumber" });
            DropTable("dbo.FixedAssets");

            DropForeignKey("dbo.MaintenanceRequests", "LinkedDocumentId", "dbo.Documents");
            DropForeignKey("dbo.MaintenanceRequests", "AssigneeEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.MaintenanceRequests", "RequesterEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.MaintenanceRequests", "RoomId", "dbo.Rooms");
            DropForeignKey("dbo.MaintenanceRequests", "BuildingId", "dbo.Buildings");
            DropIndex("dbo.MaintenanceRequests", new[] { "RegistrationDate" });
            DropIndex("dbo.MaintenanceRequests", new[] { "Status" });
            DropIndex("dbo.MaintenanceRequests", new[] { "LinkedDocumentId" });
            DropIndex("dbo.MaintenanceRequests", new[] { "AssigneeEmployeeId" });
            DropIndex("dbo.MaintenanceRequests", new[] { "RequesterEmployeeId" });
            DropIndex("dbo.MaintenanceRequests", new[] { "RoomId" });
            DropIndex("dbo.MaintenanceRequests", new[] { "BuildingId" });
            DropTable("dbo.MaintenanceRequests");

            DropForeignKey("dbo.Rooms", "ResponsibleEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.Rooms", "BuildingId", "dbo.Buildings");
            DropIndex("dbo.Rooms", new[] { "ResponsibleEmployeeId" });
            DropIndex("dbo.Rooms", "IX_Rooms_Building_Number");
            DropTable("dbo.Rooms");

            DropForeignKey("dbo.Buildings", "ResponsibleEmployeeId", "dbo.Employees");
            DropIndex("dbo.Buildings", new[] { "ResponsibleEmployeeId" });
            DropIndex("dbo.Buildings", new[] { "Name" });
            DropTable("dbo.Buildings");
        }
    }
}
